using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure;
using EventBusRabbitMQ.Infrastructure.Context;
using EventBusRabbitMQ.Infrastructure.EventBus;
using EventBusRabbitMQ.Infrastructure.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventBusExtensions
	{

	public static IEventBusBuilder AddRabbitMqEventBus(this IHostApplicationBuilder builder, string connectionName)
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.AddRabbitMQClient(connectionName, configureConnectionFactory: factory =>
		{
			(factory).DispatchConsumersAsync = true;
		});

		builder.Services.AddHostedService<OutboxWorker<EventBusDbContext>>();

		builder.Services.AddSingleton<IResiliencePipelineProvider, ResiliencePipelineFactory>();
		builder.Services.AddScoped<IMessageDeduplicationService, MessageDeduplicationService>();
		builder.Services.AddSingleton<IRabbitMQPersistentConnection, RabbitMQPersistentConnection>();

		builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();
		builder.Services.AddSingleton<IEventBus, EventBus>();
		builder.Services.AddSingleton<IHostedService>(sp =>
			(EventBus)sp.GetRequiredService<IEventBus>());


		builder.Services.AddSingleton<EventBusSubscriptionInfo>();

		return new EventBusBuilder(builder.Services);
	
	}

	private class EventBusBuilder(IServiceCollection services) : IEventBusBuilder
	{
		public IServiceCollection Services => services;
	}
	public static IEventBusBuilder AddEventDbContext<TDbContext>(
	this IEventBusBuilder builder,
	string connectionString)
	where TDbContext : DbContext , IEventStoreDbContext
	{
		ArgumentNullException.ThrowIfNull(builder);

		builder.Services.AddDbContext<EventBusDbContext>((serviceProvider, options) =>
		{
			var context = serviceProvider.GetRequiredService<TDbContext>();
			options.UseNpgsql(context.Database.GetDbConnection())
				  .ConfigureWarnings(warnings => warnings.Ignore(
					  RelationalEventId.PendingModelChangesWarning));
		});	

		builder.Services.AddDbContextFactory<EventBusDbContext>((provider, options) =>
		{

			options.UseNpgsql(connectionString)
				   .ConfigureWarnings(warnings =>
					   warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
		}, lifetime: ServiceLifetime.Scoped);

		builder.Services.AddScoped<ITransactionalOutbox, TransactionalOutbox<TDbContext>>();

		builder.Services.AddNpgsqlDataSource(connectionString, dataSourceBuilder =>
		{
			dataSourceBuilder.EnableParameterLogging();
		});

		return new EventBusBuilder(builder.Services);
	}

	// when eventstore dbset are not part of dbcontext
	public static IEventBusBuilder AddSeeder<TDbContext>(
	this IEventBusBuilder builder,
	string connectionName)
	where TDbContext : DbContext, IEventStoreDbContext
	{
		builder.Services.AddHostedService(provider =>
		{
			var logger = provider.GetRequiredService<ILogger<DatabaseSeeder>>();
			return new DatabaseSeeder(provider, logger);
		});

		return new EventBusBuilder(builder.Services);
	}

	public static IEventBusBuilder AddSubscription<TEvent, THandler>(
			this IEventBusBuilder builder)
			where TEvent : IntegrationEvent
			where THandler : class, IIntegrationEventHandler<TEvent>
		{
			builder.Services.AddKeyedTransient<IIntegrationEventHandler, THandler>(typeof(TEvent));
			builder.Services.Configure<EventBusSubscriptionInfo>(o =>
			{
				o.EventTypes[typeof(TEvent).Name] = typeof(TEvent);
			});
			return builder;
		}
	}

public interface IEventBusBuilder
	{
		IServiceCollection Services { get; }
}

