using System;
using System.Threading;
using System.Threading.Tasks;
using EventBusRabbitMQ.Infrastructure.EventBus;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

/// <summary>
/// Manages a persistent RabbitMQ connection with automatic recovery and resilience policies
/// </summary>
public sealed class RabbitMQPersistentConnection : IRabbitMQPersistentConnection, IDisposable
{
	private readonly IConnectionFactory _connectionFactory;
	private readonly IResiliencePipelineProvider _policyProvider;
	private readonly ILogger<RabbitMQPersistentConnection> _logger;
	private readonly SemaphoreSlim _connectionLock = new(1, 1);

	private IConnection? _connection;
	private bool _disposed;

	/// <summary>
	/// Indicates if the connection is currently active and open
	/// </summary>
	public bool IsConnected => _connection?.IsOpen == true && !_disposed;

	/// <summary>
	/// Initializes a new persistent RabbitMQ connection handler
	/// </summary>
	public RabbitMQPersistentConnection(
		IConnectionFactory connectionFactory,
		IResiliencePipelineProvider policyProvider,
		ILogger<RabbitMQPersistentConnection> logger)
	{
		_connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
		_policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Creates a new channel model with automatic connection recovery
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown if connection is disposed</exception>
	/// <exception cref="InvalidOperationException">Thrown if connection cannot be established</exception>
	public async Task<IModel> CreateModelAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		await EnsureConnectedAsync(cancellationToken);

		return await _policyProvider.GetChannelPolicy().ExecuteAsync(async ct =>
		{
			var channel = await Task.Run(() => _connection!.CreateModel(), ct);
			_logger.LogDebug("Created channel #{ChannelNumber}", channel.ChannelNumber);
			return channel;
		}, cancellationToken);
	}

	/// <summary>
	/// Attempts to establish a connection with retry logic
	/// </summary>
	/// <returns>True if connection succeeded, false otherwise</returns>
	public async Task<bool> TryConnectAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		await _connectionLock.WaitAsync(cancellationToken);
		try
		{
			if (IsConnected) return true;

			_logger.LogInformation("Attempting RabbitMQ connection...");
			_connection = await CreateConnectionWithRetryAsync(cancellationToken);
			SetupConnectionEvents();

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "RabbitMQ connection failed");
			return false;
		}
		finally
		{
			_connectionLock.Release();
		}
	}


	/// <summary>
	/// Ensures a valid connection exists or throws
	/// </summary>
	private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
	{
		if (!IsConnected && !await TryConnectAsync(cancellationToken))
		{
			throw new InvalidOperationException("No RabbitMQ connection available");
		}
	}

	/// <summary>
	/// Creates a connection with configured retry policy
	/// </summary>
	private async Task<IConnection> CreateConnectionWithRetryAsync(CancellationToken cancellationToken)
	{
		return await _policyProvider.GetConnectionPolicy().ExecuteAsync(async ct =>
		{
			var conn = await Task.Run(() => _connectionFactory.CreateConnection(), ct);

			if (!conn.IsOpen)
			{
				_logger.LogInformation("Successfully connected to RabbitMQ (Client: {ClientProvidedName})", conn.ClientProvidedName);
				throw new BrokerUnreachableException(new Exception("broker connection unreachable")); // or pass any inner exception if necessary
			}
			else
			{
				_logger.LogWarning("Connection to RabbitMQ failed to open (Client: {ClientProvidedName})", conn.ClientProvidedName);
			}
			return conn;
		}, cancellationToken);
	}

	/// <summary>
	/// Configures connection lifecycle event handlers
	/// </summary>
	private void SetupConnectionEvents()
	{
		if (_connection == null) return;

		_connection.ConnectionShutdown += OnConnectionShutdown;
		_connection.CallbackException += OnCallbackException;
		_connection.ConnectionBlocked += OnConnectionBlocked;
	}

	/// <summary>
	/// Handles connection shutdown events
	/// </summary>
	private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
	{
		if (_disposed) return;
		_logger.LogWarning("Connection shutdown: {ReplyText} (InitiatedBy: {Initiator})", e.ReplyText, e.Initiator);
		TryReconnect();
	}

	/// <summary>
	/// Handles callback exceptions
	/// </summary>
	private void OnCallbackException(object? sender, CallbackExceptionEventArgs e)
	{
		if (_disposed) return;
		_logger.LogWarning(e.Exception, "Connection callback exception");
		TryReconnect();
	}

	/// <summary>
	/// Handles connection blocked events
	/// </summary>
	private void OnConnectionBlocked(object? sender, ConnectionBlockedEventArgs e)
	{
		if (_disposed) return;
		_logger.LogWarning("Connection blocked: {Reason}", e.Reason);
	}

	/// <summary>
	/// Initiates automatic reconnection attempts
	/// </summary>
	private void TryReconnect()
	{
		if (_disposed) return;

		_ = Task.Run(async () =>
		{
			await Task.Delay(TimeSpan.FromSeconds(1));

			await Policy.Handle<Exception>()
				.WaitAndRetryForeverAsync(
					attempt => TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 30)),
					(ex, delay) => _logger.LogWarning(ex, "Reconnect attempt failed. Next retry in {Delay}s", delay.TotalSeconds))
				.ExecuteAsync(async () =>
				{
					bool reconnected = await TryConnectAsync();
					if (reconnected)
					{
						_logger.LogInformation("Reconnected successfully");
					}
					return reconnected;
				});
		});

	}
	/// <summary>
	/// Cleans up resources and closes the connection
	/// </summary>
	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;

		try
		{
			_connectionLock.Wait();
			try
			{
				_connection?.Close();
				_connection?.Dispose();
				_logger.LogInformation("Connection disposed");
			}
			finally
			{
				_connectionLock.Release();
			}
		}
		catch (Exception ex)
		{
			_logger.LogCritical(ex, "Error during disposal");
		}
		finally
		{
			_connectionLock.Dispose();
		}
	}
	/// <summary>
	/// Throws if the instance has been disposed
	/// </summary>
	private void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, nameof(RabbitMQPersistentConnection));
	}
}