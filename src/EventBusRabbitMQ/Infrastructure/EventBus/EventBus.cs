using System.Data;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EventBusRabbitMQ.Domain;
using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure.Messaging;
using EventBusRabbitMQ.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry.Trace;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


namespace EventBusRabbitMQ.Infrastructure.EventBus
{
	public sealed class EventBus : IEventBus
	{
		private readonly IRabbitMQPersistentConnection _persistentConnection;
		private readonly ILogger<EventBus> _logger;
		private readonly IServiceProvider _serviceProvider;
		private readonly EventBusOptions _options;
		private readonly EventBusSubscriptionInfo _subscriptionInfo;
		private IModel? _consumerChannel;
		private bool _disposed;

		public EventBus(
			IRabbitMQPersistentConnection persistentConnection,
			ILogger<EventBus> logger,
			IServiceProvider serviceProvider,
			IOptions<EventBusOptions> options,
			IOptions<EventBusSubscriptionInfo> subscriptionInfo)
		{
			_persistentConnection = persistentConnection;
			_logger = logger;
			_serviceProvider = serviceProvider;
			_options = options.Value;
			_subscriptionInfo = subscriptionInfo.Value;
		}

		public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
		where TEvent : IntegrationEvent
		{
			await using var scope = _serviceProvider.CreateAsyncScope();
			var outbox = scope.ServiceProvider.GetRequiredService<ITransactionalOutbox>();

			try
			{
				var storeResult = await outbox.StoreOutgoingMessageAsync(@event, ct);

				// If message was a duplicate, we should still publish it
				// because the outbox might be behind (eventually consistent)
				if (storeResult == MessageStoreResult.Duplicate)
				{
					_logger.LogDebug("Duplicate message detected in outbox, publishing directly: {MessageId}", @event.Id);
					await PublishDirect(@event, ct);
					return;
				}
			}
			catch (Exception ex) when (ShouldFallbackToDirectPublish(@event))
			{
				_logger.LogWarning(ex, "Outbox failed, falling back to direct publish");
				await PublishDirect(@event, ct);
			}
		}
		public async Task PublishAsync<TEvent>(TEvent @event, IDbContextTransaction ts)
		where TEvent : IntegrationEvent
		{
			await using var scope = _serviceProvider.CreateAsyncScope();
			var outbox = scope.ServiceProvider.GetRequiredService<ITransactionalOutbox>();

			try
			{
				var storeResult = await outbox.StoreOutgoingMessageAsync(@event, ts);

				// If message was a duplicate, we should still publish it
				// because the outbox might be behind (eventually consistent)
				if (storeResult == MessageStoreResult.Duplicate)
				{
					_logger.LogDebug("Duplicate message detected in outbox, publishing directly: {MessageId}", @event.Id);
					await PublishDirect(@event);
					return;
				}
			}
			catch (Exception ex) when (ShouldFallbackToDirectPublish(@event))
			{
				_logger.LogWarning(ex, "Outbox failed, falling back to direct publish");
				await PublishDirect(@event);
			}
		}
		public async Task PublishDirect<TEvent>(TEvent @event, CancellationToken ct = default)
			where TEvent : IntegrationEvent
		{
			using var channel = await _persistentConnection.CreateModelAsync(ct);
			var props = channel.CreateBasicProperties();
			RabbitMQMessageHelper.ConfigureBasicProperties(props, @event, _options.SubscriptionClientName);

			channel.ConfirmSelect();
			channel.BasicPublish(
				exchange: RabbitMQConstants.MainExchangeName,
				routingKey: typeof(TEvent).Name,
				mandatory: true,
				basicProperties: props,
				body: JsonSerializer.SerializeToUtf8Bytes(@event, @event.GetType(), _subscriptionInfo.JsonSerializerOptions));

			if (!channel.WaitForConfirms(RabbitMQConstants.DefaultConfirmTimeout))
			{
				throw new MessageNotAckedException(@event.Id);
			}
		}

		public async Task StartAsync(CancellationToken ct)
		{
			_consumerChannel = await _persistentConnection.CreateModelAsync(ct);
			ConfigureTopology(_consumerChannel);

			var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
			consumer.Received += MessagetHandler;

			_consumerChannel.BasicConsume(
				queue: _options.SubscriptionClientName,
				autoAck: false,
				consumer: consumer);

			_logger.LogInformation("Started consuming from {QueueName}", _options.SubscriptionClientName);
		}

		private void ConfigureTopology(IModel channel)
		{
			try
			{
				// Main exchange
				channel.ExchangeDeclare(
					exchange: RabbitMQConstants.MainExchangeName,
					type: ExchangeType.Direct,
					durable: true,
					autoDelete: false);

				// Dead letter exchange 
				channel.ExchangeDeclare(
					exchange: RabbitMQConstants.DeadLetterExchangeName,
					type: ExchangeType.Direct,
					durable: true,
					autoDelete: false);

				// Main queue
				var queueArgs = new Dictionary<string, object>
				{
					["x-dead-letter-exchange"] = RabbitMQConstants.DeadLetterExchangeName,
					["x-message-ttl"] = _options.MessageTTL,
					//["x-delivery-limit"] = _options.RetryCount + 1 // Total attempts = retries + initial
				};

				channel.QueueDeclare(
					queue: _options.SubscriptionClientName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: queueArgs);

				// DLQ 
				var dlqArgs = new Dictionary<string, object>
				{
					["x-queue-mode"] = "lazy" // Better for DLQs with potentially large messages
				};

				channel.QueueDeclare(
					queue: $"{_options.SubscriptionClientName}_dlq",
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: dlqArgs);


				foreach (var (eventName, eventType) in _subscriptionInfo.EventTypes)
				{
					// Main queue binding
					channel.QueueBind(
						queue: _options.SubscriptionClientName,
						exchange: RabbitMQConstants.MainExchangeName,
						routingKey: eventName);

					// DLQ binding with same routing key
					channel.QueueBind(
						queue: $"{_options.SubscriptionClientName}_dlq",
						exchange: RabbitMQConstants.DeadLetterExchangeName,
						routingKey: eventName);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to configure RabbitMQ topology");
			}

		}
		public async Task ValidateTopologyAsync(CancellationToken ct)
		{
			using var channel = await _persistentConnection.CreateModelAsync(ct);

			try
			{
				channel.ExchangeDeclarePassive(RabbitMQConstants.MainExchangeName);
				channel.ExchangeDeclarePassive(RabbitMQConstants.DeadLetterExchangeName);
				channel.QueueDeclarePassive(_options.SubscriptionClientName);
				channel.QueueDeclarePassive($"{_options.SubscriptionClientName}_dlq");

				_logger.LogInformation("RabbitMQ topology validated successfully");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "RabbitMQ topology validation failed");
				throw;
			}
		}
		public async Task ResetTopologyAsync(CancellationToken ct = default)
		{
			if (_consumerChannel?.IsOpen == true)
			{
				try
				{
					_consumerChannel.QueueDelete(_options.SubscriptionClientName, ifUnused: false, ifEmpty: false);
					_consumerChannel.QueueDelete($"{_options.SubscriptionClientName}_dlq", ifUnused: false, ifEmpty: false);
					_consumerChannel.ExchangeDelete(RabbitMQConstants.MainExchangeName, ifUnused: false);
					_consumerChannel.ExchangeDelete(RabbitMQConstants.DeadLetterExchangeName, ifUnused: false);
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error resetting RabbitMQ topology");
					throw;
				}
			}

			await InitializeConsumer(ct);
		}
		private async Task MessagetHandler(object sender, BasicDeliverEventArgs args)
		{
			var eventName = args.RoutingKey;
			var messageId = RabbitMQMessageHelper.GetMessageId(args);
			var deliveryTag = args.DeliveryTag;
			var retryCount = RabbitMQMessageHelper.GetRetryCount(args);
			var attemptNumber = retryCount + 1;

			using var activity = StartActivity(args);
			try
			{
				await using var scope = _serviceProvider.CreateAsyncScope();
				var processor = scope.ServiceProvider.GetRequiredService<IMessageProcessor>();


				var result = await processor.ProcessMessageAsync(args, _options.EnableDeduplication);

				if (result == ProcessingResult.Success)
				{
					SafeAck(deliveryTag);

					activity?.SetTag("message.status", "processed");
					_logger.LogInformation("Processed message {MessageId}", messageId);
				}
				
				else if (result == ProcessingResult.RetryLater && attemptNumber < _options.RetryCount + 1)
				{
					var delay = CalculateRetryDelay(attemptNumber);
					args.BasicProperties.IncrementRetryCount();
					args.BasicProperties.Headers["x-retry-delay"] = delay.TotalMilliseconds;

					SafeNack(deliveryTag, requeue: true);

					activity?.SetTag("message.status", "retrying");
					activity?.SetTag("message.retry_delay_ms", delay.TotalMilliseconds);

					_logger.LogWarning("Retrying message {MessageId} (Attempt {AttemptNumber}/{MaxAttempts}) in {Delay}ms",
						messageId, attemptNumber, _options.RetryCount + 1, delay.TotalMilliseconds);
				}
				else
				{

					AddFailureDetails(args.BasicProperties, result);

					SafeNack(deliveryTag, requeue: false);

					activity?.SetTag("message.status", "failed");
					_logger.LogError("Message {MessageId} moved to DLQ after {AttemptNumber} attempts",
						messageId, attemptNumber);
				}
			}
			catch (Exception ex)
			{
				activity?.RecordException(ex);

				AddFailureDetails(args.BasicProperties, ProcessingResult.PermanentFailure, ex);

				_logger.LogError(ex, "Critical error processing message {MessageId}", messageId);
				SafeNack(deliveryTag, requeue: false);
			}
		}

		private TimeSpan CalculateRetryDelay(int attemptNumber)
		{
			// Exponential backoff with jitter
			var maxDelay = TimeSpan.FromMinutes(5);
			var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attemptNumber));
			var jitter = new Random().NextDouble() * 0.2; // ±20% jitter
			var delay = baseDelay * (1 + jitter);

			return delay > maxDelay ? maxDelay : delay;
		}

		private void AddFailureDetails(IBasicProperties properties, ProcessingResult result, Exception? ex = null)
		{
			properties.Headers ??= new Dictionary<string, object>();

			properties.Headers["x-failure-reason"] = result.ToString();
			properties.Headers["x-failure-timestamp"] = DateTime.UtcNow.ToString("O");

			if (ex != null)
			{
				properties.Headers["x-exception-type"] = ex.GetType().Name;
				properties.Headers["x-exception-message"] = ex.Message;
				properties.Headers["x-exception-stacktrace"] = ex.StackTrace;
			}
		}

		private bool ShouldRetry(ProcessingResult result, int retryCount) =>
			result == ProcessingResult.RetryLater && retryCount < _options.RetryCount;

		private Activity? StartActivity(BasicDeliverEventArgs args)
		{
			var activity = new ActivitySource("EventBus").StartActivity("ProcessMessage");
			activity?.SetTag("message.id", args.BasicProperties.MessageId);
			activity?.SetTag("message.routing_key", args.RoutingKey);
			activity?.SetTag("message.retry_count", RabbitMQMessageHelper.GetRetryCount(args));
			return activity;
		}

		private void SafeAck(ulong deliveryTag)
		{
			try
			{
				_consumerChannel?.BasicAck(deliveryTag, multiple: false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to ACK message");
			}
		}

		private void SafeNack(ulong deliveryTag, bool requeue)
		{
			try
			{
				_consumerChannel?.BasicNack(deliveryTag, multiple: false, requeue: requeue);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to NACK message");
			}
		}

		private bool ShouldFallbackToDirectPublish<TEvent>(TEvent @event) where TEvent : IntegrationEvent =>
			@event is IAllowDirectFallback;
		private async Task InitializeConsumer(CancellationToken ct)
		{
			_consumerChannel = await _persistentConnection.CreateModelAsync(ct);
			ConfigureTopology(_consumerChannel);

			var consumer = new AsyncEventingBasicConsumer(_consumerChannel);
			consumer.Received += MessagetHandler;

			_consumerChannel.BasicConsume(
				queue: _options.SubscriptionClientName,
				autoAck: false,
				consumer: consumer);
		}

		private async void OnConnectionRecovered(object? sender, EventArgs e)
		{
			try
			{
				await InitializeConsumer(CancellationToken.None);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to reinitialize consumer after connection recovery");
			}
		}
		public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

		public void Dispose()
		{
			if (_disposed) return;

			_disposed = true;
			try
			{
				_consumerChannel?.Dispose();
				_logger.LogInformation("RabbitMQ event bus disposed");
			}
			catch (Exception ex)
			{
				_logger.LogCritical(ex, "Error disposing consumer channel");
			}
		}
		public IModel? GetConsumerChannel()
		{
			return _consumerChannel;
		}
	}

	public static class RabbitMQMessageHelper
	{
		public static void ConfigureBasicProperties(
			IBasicProperties properties,
			IntegrationEvent @event,
			string serviceName)
		{
			properties.DeliveryMode = RabbitMQConstants.PersistentDeliveryMode;
			properties.MessageId = @event.Id.ToString();
			properties.Headers = new Dictionary<string, object>
			{
				[RabbitMQConstants.EventTypeHeader] = @event.GetType().Name,
				[RabbitMQConstants.OccurredOnHeader] = @event.CreationDate.ToString("O"),
				[RabbitMQConstants.SourceServiceHeader] = serviceName,
				["x-retry-count"] = 0 
			};
		}

		public static Guid GetMessageId(BasicDeliverEventArgs args) =>
			Guid.Parse(args.BasicProperties.MessageId);

		public static int GetRetryCount(BasicDeliverEventArgs args)
		{
			if (args.BasicProperties.Headers?.TryGetValue("x-retry-count", out var value) == true)
			{
				return value is int count ? count : 0;
			}
			return 0;
		}

		public static void IncrementRetryCount(this IBasicProperties properties)
		{
			var current = GetRetryCount(properties);
			properties.Headers["x-retry-count"] = current + 1;
		}

		private static int GetRetryCount(IBasicProperties properties)
		{
			if (properties.Headers?.TryGetValue("x-retry-count", out var value) == true)
			{
				return value is int count ? count : 0;
			}
			return 0;
		}
	}

	public class ErrorProcessingContext
	{
		public int AttemptNumber { get; set; }
		public int MaxAttempts { get; set; }
		public IDictionary<string, object>? Headers { get; set; }
	}

}