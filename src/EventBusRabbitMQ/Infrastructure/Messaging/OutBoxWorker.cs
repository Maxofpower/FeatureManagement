using Dapper;
using EventBusRabbitMQ.Domain;
using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure;
using EventBusRabbitMQ.Infrastructure.EventBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Text.Json;


public class OutboxWorker<TDbContext> : BackgroundService where TDbContext : DbContext
{
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<OutboxWorker<TDbContext>> _logger;
	private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
	private NpgsqlDataSource _dataSource;
	private readonly int BatchSize = 20;
	private readonly int _maxRetryCount = 3;
	private const int MaxErrorLength = 500;


	public OutboxWorker(IServiceProvider serviceProvider, ILogger<OutboxWorker<TDbContext>> logger
		, NpgsqlDataSource dataSource)
	{
		_serviceProvider = serviceProvider;
		_logger = logger;
		_dataSource = dataSource;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessPendingMessagesAsync(stoppingToken);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				_logger.LogError(ex, "Error processing outbox messages");
			}
			catch(Exception ex)
			{
				_logger.LogError(ex, "dapper query exception");
			}

			await Task.Delay(_interval, stoppingToken);
		}
	}

	// with database supporting index
	//private static readonly Func<TDbContext, CancellationToken, Task<List<OutboxMessage>>> _getPendingMessages =
	//	EF.CompileAsyncQuery((TDbContext ctx, CancellationToken ct) =>
	//		ctx.Set<OutboxMessage>()
	//			.Where(m => m.Status == MessageStatus.Pending ||
	//					   (m.Status == MessageStatus.Failed && m.RetryCount < 3))
	//			.OrderBy(m => m.CreatedAt)
	//			.Take(200)
	//			.ToList());

	private async Task ProcessPendingMessagesAsync(CancellationToken stoppingToken)
	{
		using var scope = _serviceProvider.CreateScope();
		var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>(); // Use the service's specific DbContext
		var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();
		var subscriptionInfo = scope.ServiceProvider.GetRequiredService<IOptions<EventBusSubscriptionInfo>>();

		
		var messages = await QueryPendingMessagesAsync(dbContext, stoppingToken);
	

		foreach (var message in messages)
		{
			try
			{
				if (!subscriptionInfo.Value.EventTypes.TryGetValue(message.EventType, out var eventType))
				{
					await MarkMessageAsFailedAsync(dbContext, message,
						$"Event type '{message.EventType}' not found in subscriptions or assemblies",
						stoppingToken);
					continue;
				}

				await MarkMessageAsProcessingAsync(dbContext, message, stoppingToken);

				// Deserialize the message payload into the resolved event type
				var @event = JsonSerializer.Deserialize(message.Payload, eventType,
					subscriptionInfo.Value.JsonSerializerOptions) as IntegrationEvent;

				if (@event == null || @event.Id != message.Id)
				{
					await MarkMessageAsFailedAsync(dbContext, message,
						$"ID mismatch or null event (Stored: {message.Id}, Deserialized: {@event?.Id})",
						stoppingToken);
					continue;
				}

				if (@event is not IntegrationEvent integrationEvent)
				{
					await MarkMessageAsFailedAsync(dbContext, message,
						$"Deserialized object is not an IntegrationEvent (Type: {eventType.Name})",
						stoppingToken);
					continue;
				}

				await eventBus.PublishDirect((dynamic)@event, ct: stoppingToken);

				await MarkMessageAsProcessedAsync(dbContext, message, stoppingToken);
			}
			catch (JsonException jsonEx)
			{
				await HandleProcessingFailureAsync(dbContext, message,
					new InvalidOperationException($"JSON deserialization failed for {message.EventType}", jsonEx),
					stoppingToken);
			}
			catch (Exception ex)
			{
				await HandleProcessingFailureAsync(dbContext, message, ex, stoppingToken);
			}
		}
	}
	private async Task<List<OutboxMessage>> QueryPendingMessagesAsync(TDbContext dbContext,	CancellationToken ct)
	{
		await using var connection = await _dataSource.OpenConnectionAsync(ct);
		await using var transaction = await connection.BeginTransactionAsync(ct);
		 var result = (await connection.QueryAsync<OutboxMessage>(
		@"
        SELECT id AS Id, event_type AS EventType, 
         payload::text::bytea AS Payload
       , created_at AS CreatedAt, retry_count AS RetryCount
        FROM outbox_messages
        WHERE processed_at IS NULL
        ORDER BY created_at 
        LIMIT @BatchSize
        ",
		new { BatchSize}, 
		transaction: transaction)).AsList();
		return result;
	}

	private async Task MarkMessageAsProcessingAsync(
		TDbContext dbContext,
		OutboxMessage message,
		CancellationToken ct)
	{
		message.Status = MessageStatus.Processing;
		message.ProcessedAt = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(ct);
	}

	private async Task MarkMessageAsProcessedAsync(
		TDbContext dbContext,
		OutboxMessage message,
		CancellationToken ct)
	{
		message.Status = MessageStatus.Processed;
		message.CompletedAt = DateTime.UtcNow;
		message.Error = null;
		await dbContext.SaveChangesAsync(ct);
	}

	private async Task MarkMessageAsFailedAsync(
		TDbContext dbContext,
		OutboxMessage message,
		string error,
		CancellationToken ct)
	{
		message.Status = MessageStatus.Failed;
		message.Error = error.Length > MaxErrorLength ? error[..MaxErrorLength] : error;
		message.RetryCount++;
		await dbContext.SaveChangesAsync(ct);
	}

	private async Task HandleProcessingFailureAsync(
		TDbContext dbContext,
		OutboxMessage message,
		Exception ex,
		CancellationToken ct)
	{
		_logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
		await MarkMessageAsFailedAsync(dbContext, message, ex.Message, ct);
	}

	private Type? ResolveEventType(string eventTypeName, EventBusSubscriptionInfo info)
	{
		if (info.EventTypes.TryGetValue(eventTypeName, out var knownType))
			return knownType;

		// Fallback: scan all loaded assemblies
		var allTypes = AppDomain.CurrentDomain.GetAssemblies()
			.SelectMany(a =>
			{
				try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
			})
			.Where(t => typeof(IntegrationEvent).IsAssignableFrom(t) && !t.IsAbstract)
			.ToList();

		return allTypes.FirstOrDefault(t => t.Name == eventTypeName);
	}
}