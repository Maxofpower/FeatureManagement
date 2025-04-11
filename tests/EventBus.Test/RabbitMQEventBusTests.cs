using EventBusRabbitMQ.Events;
using EventBusRabbitMQ.Infrastructure;
using EventBusRabbitMQ;
using FeatureFusion.Features.Order.IntegrationEvents.Events;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using EventBusRabbitMQ.Infrastructure.EventBus;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace EventBus.Tests;

public class RabbitMQEventBusIntegrationTests : IClassFixture<RabbitMQFixture>, IAsyncLifetime
{
	private readonly RabbitMQFixture _fixture;
	private readonly WebApplicationFactory<Program> _webApplicationFactory;
	private readonly IServiceProvider _services;

	public RabbitMQEventBusIntegrationTests(RabbitMQFixture fixture)
	{
		_fixture = fixture;
		_webApplicationFactory = fixture.WithWebHostBuilder(builder =>
		{
			builder.ConfigureServices(services =>
			{
				services.Configure<EventBusOptions>(options =>
				{
					options.EnableDeduplication = false;
					options.SubscriptionClientName = "feature_fusion";
					options.RetryCount = 3;
				});
			});
		});
		_services = _webApplicationFactory.Services;
	}

	public async Task InitializeAsync() => await _fixture.ResetRabbitMQ();
	public Task DisposeAsync() => Task.CompletedTask;

	[Fact]
	public async Task Publishes_And_Processes_Events()
	{
		await TestEventProcessing<OrderCreatedIntegrationEvent>(
			() => new OrderCreatedIntegrationEvent(Guid.NewGuid(), 99.0m));
	}

	[Fact]
	public async Task Publishes_And_Processes_Failed_Events()
	{
		await _fixture.ResetRabbitMQ();
		var testEvent = new FailingIntegrationEvent(Guid.NewGuid(), 110.0m);
		await PublishAndVerifyDlq(testEvent);
	}

	[Fact]
	public async Task Verify_Message_Flow()
	{
		await _fixture.ResetRabbitMQ();
		var testEvent = new OrderCreatedIntegrationEvent(Guid.NewGuid(), 99.0m);
		await VerifyMessageFlow(testEvent, "OrderCreatedIntegrationEvent");
	}

	[Fact]
	public async Task Processes_Duplicate_Message_Only_Once()
	{
		// Arrange
		_fixture.ProcessedEvents.Clear();
		var eventBus = GetRequiredService<IEventBus>();
		var testEvent = new OrderCreatedIntegrationEvent(Guid.NewGuid(), 100.0m);

		// Act - First publish
		await eventBus.PublishAsync(testEvent);
		await Wait.UntilAsync(() => _fixture.ProcessedEvents.Any(), TimeSpan.FromSeconds(20));

		// Assert - Processed once
		_fixture.ProcessedEvents.Should().ContainSingle(e => e.Id == testEvent.Id);

		// Act - Second publish
		_fixture.ProcessedEvents.Clear();
		await eventBus.PublishAsync(testEvent);
		await Task.Delay(50000); // Brief delay

		// Assert - Not processed again
		_fixture.ProcessedEvents.Should().BeEmpty();
	}

	[Fact]
	public async Task Should_Not_Requeue_Invalid_Messages()
	{
		using var channel = await CreateChannelAsync();
		var dlqName = GetDlqName();

		channel.QueuePurge(dlqName);

		var props = channel.CreateBasicProperties();
		props.MessageId = Guid.NewGuid().ToString();
		props.Headers = new Dictionary<string, object>
		{
			[RabbitMQConstants.EventTypeHeader] = "NonExistentEventType",
			[RabbitMQConstants.SourceServiceHeader] = "TestService"
		};

		channel.BasicPublish(
			exchange: RabbitMQConstants.MainExchangeName,
			routingKey: "OrderCreatedIntegrationEvent",
			mandatory: true,
			basicProperties: props,
			body: Encoding.UTF8.GetBytes("{ invalid json }"));

		await WaitForMessageCount(channel, dlqName, 1);
		var dlqMessage = channel.BasicGet(dlqName, autoAck: true);

		dlqMessage.Should().NotBeNull();
		dlqMessage!.BasicProperties.MessageId.Should().Be(props.MessageId);
	}

	#region Helper Methods

	private async Task TestEventProcessing<T>(Func<T> eventFactory) where T : IntegrationEvent
	{
		_fixture.ProcessedEvents.Clear();
		var testEvent = eventFactory();
		var eventBus = GetRequiredService<IEventBus>();

		await eventBus.PublishAsync(testEvent);
		await Wait.UntilAsync(() => _fixture.ProcessedEvents.Any(), TimeSpan.FromSeconds(20));

		_fixture.ProcessedEvents.Should().ContainSingle(e => e.Id == testEvent.Id);
	}

	private async Task VerifyMessageFlow(OrderCreatedIntegrationEvent testEvent, string routingKey)
	{
		using var channel = await CreateChannelAsync();
		var testQueue = "test_feature_fusion";

		channel.QueueDeclare(testQueue, durable: true, exclusive: false, autoDelete: false);
		channel.QueueBind(testQueue, RabbitMQConstants.MainExchangeName, routingKey);

		await GetRequiredService<IEventBus>().PublishAsync(testEvent);
		await WaitForMessageCount(channel, testQueue, 1);

		var message = channel.BasicGet(testQueue, autoAck: false);
		message.Should().NotBeNull();
		message!.BasicProperties.MessageId.Should().Be(testEvent.Id.ToString());

		channel.QueueDelete(testQueue);
	}

	private async Task PublishAndVerifyDlq(IntegrationEvent testEvent)
	{
		
		using var channel = await CreateChannelAsync();
		var dlqName = GetDlqName();
		channel.QueuePurge(dlqName);
		await GetRequiredService<IEventBus>().PublishAsync(testEvent);

		var foundMessage = await WaitForMessageByIdAsync(
			channel: channel,
			queueName: dlqName,
			testEvent.Id,
			acknowledgeIfFound: true,
			timeout: TimeSpan.FromSeconds(60));

		foundMessage.Should().NotBeNull();
		var deserialized = JsonSerializer.Deserialize(
			Encoding.UTF8.GetString(foundMessage!.Body.Span),
			testEvent.GetType());

		deserialized.Should().BeEquivalentTo(testEvent);
	}

	private async Task<IModel> CreateChannelAsync() =>
		await GetRequiredService<IRabbitMQPersistentConnection>().CreateModelAsync();

	private string GetDlqName() =>
		$"{GetRequiredService<IOptions<EventBusOptions>>().Value.SubscriptionClientName}_dlq";

	private T GetRequiredService<T>() where T : notnull =>
		_services.GetRequiredService<T>();

	private async Task WaitForMessageCount(
		IModel channel,
		string queueName,
		int expectedCount,
		TimeSpan? timeout = null)
	{
		var timeoutValue = timeout ?? TimeSpan.FromSeconds(30);
		var sw = Stopwatch.StartNew();

		while (!channel.IsClosed && sw.Elapsed < timeoutValue)
		{
			if (channel.QueueDeclarePassive(queueName).MessageCount >= expectedCount)
				return;

			await Task.Delay(200);
		}

		throw new TimeoutException($"Queue '{queueName}' didn't reach {expectedCount} messages");
	}

	private async Task<BasicGetResult> WaitForMessageByIdAsync(
		IModel channel,
		string queueName,
		Guid expectedMessageId,
		bool acknowledgeIfFound,
		TimeSpan timeout)
	{
		var startTime = DateTime.UtcNow;

		while (DateTime.UtcNow - startTime < timeout)
		{
			var message = channel.BasicGet(queueName, autoAck: false);
			if (message != null)
			{
				if (Guid.Parse(message.BasicProperties.MessageId) == expectedMessageId)
				{
					if (acknowledgeIfFound) channel.BasicAck(message.DeliveryTag, multiple: false);
					return message;
				}
				channel.BasicNack(message.DeliveryTag, multiple: false, requeue: true);
			}
			await Task.Delay(200);
		}
		return null;
	}

	#endregion
}

#region Extensions and DTOs
public record TestIntegrationEvent : IntegrationEvent;

public class TestIntegrationEventHandler : IIntegrationEventHandler<TestIntegrationEvent>
{
	public List<TestIntegrationEvent> ReceivedEvents { get; } = new();

	public Task Handle(TestIntegrationEvent @event)
	{
		ReceivedEvents.Add(@event);
		return Task.CompletedTask;
	}
}
public record FailingIntegrationEvent : IntegrationEvent
{
	public decimal Total { get; }

	public FailingIntegrationEvent(Guid id, decimal total)
	{
		Id = id;
		Total = total;
	}
}

public record FailingIntegrationEventHandler : IIntegrationEventHandler<FailingIntegrationEvent>
{
	public Task Handle(FailingIntegrationEvent @event)
	{
		throw new InvalidOperationException("Simulated handler failure");
	}
}
public static class Wait
{
	public static async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
	{
		var stopwatch = Stopwatch.StartNew();
		while (stopwatch.Elapsed < timeout)
		{
			if (condition()) return;
			await Task.Delay(100);
		}
		throw new TimeoutException("Condition not met within timeout");
	}
}

#endregion