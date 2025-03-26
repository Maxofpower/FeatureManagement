using Xunit;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using global::FeatureFusion.Features.Order.Behavior;
using global::FeatureFusion.Features.Order.Commands;
using global::FeatureFusion.Infrastructure.CQRS;
using Microsoft.Extensions.DependencyInjection;
using FeatureFusion.Models;
using static FeatureFusion.Features.Order.Commands.CreateOrderCommandHandler;

namespace FeatureFusion.Infrastructure.CQRS.Tests
{
	public class MediatorTests
	{
		private readonly CreateOrderCommand _validCommand = new()
		{
			ProductId = 12345,
			Quantity = 2,
			CustomerId = 11111
		};

		[Fact]
		public async Task Send_CreateOrderCommand_ShouldReturnValidOrderResponse()
		{
			// Arrange
			var services = new TestServiceProvider()
				.AddHandler(new CreateOrderCommandHandler());

			var mediator = new Mediator(services.Build());

			// Act
			var result = await mediator.Send(_validCommand);

			// Assert
			Assert.Equal("John Doe", result.CustomerName);
			Assert.Equal("Smartphone", result.ProductName);
			Assert.Equal(2, result.Quantity);
			Assert.Equal(599.99m * 2, result.TotalAmount);
		}

		[Fact]
		public async Task Send_CreateOrderCommand_WithLogging_ShouldLogBeforeAndAfter()
		{
			// Arrange
			var logger = new TestLogger<LoggingBehavior<CreateOrderCommand, OrderResponse>>();

			var services = new TestServiceProvider()
				.AddHandler(new CreateOrderCommandHandler())
				.AddBehavior(new LoggingBehavior<CreateOrderCommand, OrderResponse>(logger));

			var mediator = new Mediator(services.Build());

			// Act
			var result = await mediator.Send(_validCommand);

			// Assert
			var (requestLogLevel, requestLogMessage) = logger.LogEntries[0];
			Assert.Equal(LogLevel.Information, requestLogLevel);
			Assert.Contains($"Handling request of type {nameof(CreateOrderCommand)}", requestLogMessage);

			var (responseLogLevel, responseLogMessage) = logger.LogEntries[1];
			Assert.Equal(LogLevel.Information, responseLogLevel);
			Assert.Contains($"Handled request of type {nameof(CreateOrderCommand)}", responseLogMessage);
		}

		[Fact]
		public async Task Send_CommandWithoutResponse_ShouldExecuteSuccessfully()
		{
			// Arrange
			var services = new TestServiceProvider()
				.AddHandler(new TestCommandHandler());

			var mediator = new Mediator(services.Build());
			var command = new TestCommand();

			// Act & Assert
			await mediator.Send(command); // Should not throw
		}

		[Fact]
		public async Task Send_InvalidCommand_ShouldThrowException()
		{
			// Arrange
			var services = new TestServiceProvider(); // No handlers
			var mediator = new Mediator(services.Build());

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => mediator.Send(_validCommand));
		}

		#region Test Helpers
		private class TestServiceProvider
		{
			private readonly ServiceCollection _services = new();

			public TestServiceProvider AddHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
		where TRequest : IRequest<TResponse>
			{
				_services.AddSingleton(typeof(IRequestHandler<TRequest, TResponse>), handler);
				return this;
			}

			public TestServiceProvider AddHandler<TRequest>(IRequestHandler<TRequest> handler)
				where TRequest : IRequest
			{
				_services.AddSingleton(typeof(IRequestHandler<TRequest>), handler);
				return this;
			}

			public TestServiceProvider AddBehavior<TRequest, TResponse>(IPipelineBehavior<TRequest, TResponse> behavior)
			{
				_services.AddSingleton(behavior);
				return this;
			}

			public IServiceProvider Build() => _services.BuildServiceProvider();
		}

		private class TestLogger<T> : ILogger<T>
		{
			public List<(LogLevel Level, string Message)> LogEntries { get; } = new();

			// Explicit interface implementation to match nullability constraints
			IDisposable ILogger.BeginScope<TState>(TState state) => new NullScope();

			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
				Exception? exception, Func<TState, Exception?, string> formatter)
			{
				LogEntries.Add((logLevel, formatter(state, exception)));
			}

			private class NullScope : IDisposable
			{
				public void Dispose() { }
			}
		}

		private class TestCommand : IRequest { }

		private class TestCommandHandler : IRequestHandler<TestCommand>
		{
			public Task Handle(TestCommand request, CancellationToken cancellationToken)
				=> Task.CompletedTask;
		}
		#endregion
	}
}