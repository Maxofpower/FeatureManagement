using EventBusRabbitMQ.Events;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using System;

namespace EventBusRabbitMQ.Infrastructure.EventBus
{
	public partial interface IEventBus : IHostedService, IDisposable
	{
		/// <summary>
		/// Publishes an integration event to the message broker
		/// </summary>
		/// <typeparam name="TEvent">Type of the integration event</typeparam>
		/// <param name="event">The event to publish</param>
		/// <param name="useOutbox">Whether to use the outbox pattern</param>
		/// <param name="ct">Cancellation token</param>
		Task PublishAsync<TEvent>(TEvent @event,
			CancellationToken ct = default)
			where TEvent : IntegrationEvent;
		Task PublishAsync<TEvent>(TEvent @event,
			IDbContextTransaction transaction)
	where TEvent : IntegrationEvent;
		Task PublishDirect<TEvent>(TEvent @event,
			CancellationToken ct = default)
			where TEvent : IntegrationEvent;
		IModel? GetConsumerChannel();
		Task ResetTopologyAsync(CancellationToken ct = default);
		Task ValidateTopologyAsync(CancellationToken ct = default);

	}

}