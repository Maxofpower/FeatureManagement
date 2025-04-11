using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBusRabbitMQ.Infrastructure
{
	public static class RabbitMQConstants
	{
		// Exchanges
		public const string MainExchangeName = "domain_events";
		public const string DeadLetterExchangeName = "domain_events_dlx";
		public const string OutboxExchangeName = "domain_events_outbox";

		// Queue Settings
		public const int DefaultMessageTTL = 86400000; // 24 hours in ms
		public const int DefaultPrefetchCount = 10;

		// Message Properties
		public const byte PersistentDeliveryMode = 2;
		public static readonly TimeSpan DefaultConfirmTimeout = TimeSpan.FromSeconds(10);

		// Header Names
		public const string EventTypeHeader = "Event-Type";
		public const string OccurredOnHeader = "Occurred-On";
		public const string SourceServiceHeader = "Source-Service";
		public const string MessageIdHeader = "Message-Id";
		public const string RetryCountHeader = "Retry-Count";

	}
}
