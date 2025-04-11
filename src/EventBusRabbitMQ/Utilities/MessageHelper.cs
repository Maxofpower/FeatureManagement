using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EventBusRabbitMQ.Utilities
{
	public static class MessageHelper
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
				["x-retry-count"] = 0  // Initialize retry counter
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
	public class MessageNotAckedException : Exception
	{
		public Guid MessageId { get; }

		public MessageNotAckedException(Guid messageId)
			: base($"Message {messageId} was not acknowledged by broker")
		{
			MessageId = messageId;
		}
	}
	public static class DbExceptionExtensions
	{
		public static bool IsDuplicateKeyError(this DbUpdateException ex)
		{
			return ex.InnerException is PostgresException pgEx &&
				   pgEx.SqlState == "23505"; // PostgreSQL duplicate key error code

		}
	}
	public class BusinessException : Exception
	{
		public BusinessException(string message) : base(message) { }
	}

	public class TransientException : Exception
	{
		public TransientException(string message) : base(message) { }
		public TransientException(string message, Exception inner) : base(message, inner) { }
	}
}
