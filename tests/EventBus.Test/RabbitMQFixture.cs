using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using EventBusRabbitMQ;
using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure;
using EventBusRabbitMQ.Infrastructure.Context;
using EventBusRabbitMQ.Infrastructure.EventBus;
using FeatureFusion.Features.Order.IntegrationEvents.EventHandling;
using FeatureFusion.Features.Order.IntegrationEvents.Events;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Threading;

namespace EventBus.Test;

public sealed class RabbitMQFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
	private DistributedApplication _app;
	private readonly string _rabbitMqConnectionString;
	private readonly int _retryCount = 10;
	private readonly TimeSpan _retryDelay = TimeSpan.FromSeconds(2);

	public string Host => "localhost";
	public int Port => 5672;
	public List<OrderCreatedIntegrationEvent> ProcessedEvents { get; } = new();
	public RabbitMQFixture()
	{

		_rabbitMqConnectionString = $"amqp://guest:guest@{Host}:{Port}";
		var options = new DistributedApplicationOptions { AssemblyName = typeof(RabbitMQFixture).Assembly.FullName, DisableDashboard = true };
		options.DisableDashboard = true;
		var appBuilder = DistributedApplication.CreateBuilder(options);

		var rabbitmq = appBuilder.AddRabbitMQ("eventbusTest", port: 5672)
			.WithContainerName("eventbusTest")
			.WithEnvironment("RABBITMQ_DEFAULT_USER", "guest")
			.WithEnvironment("RABBITMQ_DEFAULT_PASS", "guest")

			.WithEnvironment("RABBITMQ_DEFAULT_PERMISSIONS", ".* .* .*")
			//.WithEndpoint(5672, targetPort: Port, name: "amqp")
			.WithEndpoint(port: 15672, targetPort: 15672, name: "management");

		var username = appBuilder.AddParameter("username", secret: true, value: "username");
		var password = appBuilder.AddParameter("password", secret: true, value: "password");

		var postgres = appBuilder.AddPostgres("postgresTest", userName: username, password: password)
			 .WithContainerName("postgresTest")
			 .WithPgAdmin(container =>
			 {
				 container.WithEnvironment("PGADMIN_DEFAULT_EMAIL", "guest@admin.com");
				 container.WithEnvironment("PGADMIN_DEFAULT_PASSWORD", "guest");
			 })
			.WithEndpoint(5432, targetPort: 5432, name: "postgres");

		var appDb = postgres.AddDatabase("catalogdb");

		var memcached = appBuilder.AddContainer("memcachedTest", "memcached", "alpine")
			.WithEndpoint(11211, targetPort: 11211, name: "memcached")
			.WithContainerName("memcachedTest");


		var redis = appBuilder.AddRedis("redisTest", port: 6379)
			.WithContainerName("redisTest")
			 .WithEndpoint(targetPort: 6379, name: "redis").WithEnvironment("REDIS_OPTIONS", "--bind 0.0.0.0 --protected-mode no")
			.WithDataVolume("redis_data")
			 .WithEnvironment("REDIS_HOST", "localhost");



		_app = appBuilder.Build();
	}
	protected override IHost CreateHost(IHostBuilder builder)
	{
	
		// to test events
		builder.ConfigureServices(services =>
		{
			services.AddScoped<OrderCreatedIntegrationEventHandler>();
			services.AddKeyedScoped<IIntegrationEventHandler<OrderCreatedIntegrationEvent>,
				OrderCreatedIntegrationEventHandler>(typeof(OrderCreatedIntegrationEvent));

			services.AddKeyedScoped<IIntegrationEventHandler<TestIntegrationEvent>,
			TestIntegrationEventHandler>(typeof(TestIntegrationEvent));

			services.AddKeyedScoped<IIntegrationEventHandler<FailingIntegrationEvent>,
			FailingIntegrationEventHandler>(typeof(FailingIntegrationEvent));

			services.AddKeyedScoped<IIntegrationEventHandler>(
					typeof(OrderCreatedIntegrationEvent),
					(sp, key) => new TestEventHandlerDecorator(
						sp.GetRequiredService<OrderCreatedIntegrationEventHandler>(),
						ProcessedEvents));

			services.Configure<EventBusSubscriptionInfo>(o =>
			{
				o.EventTypes[typeof(OrderCreatedIntegrationEvent).Name] = typeof(OrderCreatedIntegrationEvent);
				o.EventTypes[typeof(TestIntegrationEvent).Name] = typeof(TestIntegrationEvent);
				o.EventTypes[typeof(FailingIntegrationEvent).Name] = typeof(FailingIntegrationEvent);
			});

			services.AddHostedService(provider =>
			{
				var logger = provider.GetRequiredService<ILogger<DatabaseSeeder>>();
				return new DatabaseSeeder(provider, logger);
			});
		});


		return base.CreateHost(builder);
	}

	public async Task InitializeAsync()
	{

		await _app.StartAsync();
		await WaitForRabbitMQ();
	}

	private async Task WaitForRabbitMQ()
	{
		await Task.Delay(TimeSpan.FromSeconds(30));
		var factory = new ConnectionFactory
		{
			Uri = new Uri(_rabbitMqConnectionString),
			DispatchConsumersAsync = true
		};

		for (int i = 0; i < _retryCount; i++)
		{
			try
			{
				using var connection = new ConnectionFactory()
				{
					Uri = new Uri(_rabbitMqConnectionString),
					RequestedConnectionTimeout = TimeSpan.FromSeconds(30)
				}.CreateConnection();
				using var channel = connection.CreateModel();
				if (connection.IsOpen)
				{
					return;
				}
			}
			catch when (i < _retryCount - 1)
			{
				await Task.Delay(_retryDelay);
			}
		}

		throw new InvalidOperationException($"Could not connect to RabbitMQ after {_retryCount} attempts");
	}
	public new async Task DisposeAsync()
	{
		await base.DisposeAsync();
		if (_app != null)
		{
			await _app.StopAsync();
			if (_app is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync();
			}
		}
	}
	public async Task ResetRabbitMQ()
	{
		using var scope = Services.CreateScope();
		var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

		try
		{
			await eventBus.ResetTopologyAsync();
		}
		catch
		{
			// Fallback if channel is closed
			var connection = scope.ServiceProvider.GetRequiredService<IRabbitMQPersistentConnection>();
			using var channel = await connection.CreateModelAsync();

			var options = scope.ServiceProvider.GetRequiredService<IOptions<EventBusOptions>>();
			var dlqName = $"{options.Value.SubscriptionClientName}_dlq";

			channel.QueuePurge(options.Value.SubscriptionClientName);
			channel.QueuePurge(dlqName);
		}
	}
}

#region Helper and DTOs
public class TestEventHandlerDecorator : IIntegrationEventHandler
{
	private readonly IIntegrationEventHandler _inner;
	private List<OrderCreatedIntegrationEvent> _trackedEvents;

	public TestEventHandlerDecorator(
		IIntegrationEventHandler inner,
		List<OrderCreatedIntegrationEvent> trackedEvents)
	{
		_inner = inner ?? throw new ArgumentNullException(nameof(inner));
		_trackedEvents = trackedEvents ?? throw new ArgumentNullException(nameof(trackedEvents));
	}

	public async Task Handle(IntegrationEvent @event)
	{
		if (@event is OrderCreatedIntegrationEvent orderEvent)
		{
			_trackedEvents.Add(orderEvent);
		}
		await _inner.Handle(@event);
	}
}
#endregion