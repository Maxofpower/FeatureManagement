using Xunit;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using FeatureFusion.Infrastructure.CQRS;
using FeatureFusion.Infrastructure.CQRS.Adapter;
using static FeatureFusion.Infrastructure.CQRS.Mediator;

namespace Tests.FeatureFusion.CQRS
{
	public class MediatorTests
	{
		#region Response Command Tests

		[Fact]
		public async Task Send_CommandWithResponse_ShouldReturnExpectedResult()
		{
			// Arrange
			var expectedResponse = new TestResponse { Value = "Test" };
			var handler = new TestResponseHandler(expectedResponse);

			var services = new TestServiceProvider()
				.AddHandler<TestResponseCommand, TestResponse>(handler);

			var mediator = new Mediator(services.Build());

			// Act
			var result = await mediator.Send(new TestResponseCommand());

			// Assert
			Assert.Equal(expectedResponse.Value, result.Value);
		}

		[Fact]
		public async Task Send_CommandWithResponse_WithBehavior_ShouldExecutePipeline()
		{
			// Arrange
			var logger = new TestLogger<LoggingBehavior<TestResponseCommand, TestResponse>>();
			var handler = new TestResponseHandler(new TestResponse());

			var services = new TestServiceProvider()
				.AddHandler<TestResponseCommand, TestResponse>(handler)
				.AddBehavior(new LoggingBehavior<TestResponseCommand, TestResponse>(logger));
			

			var mediator = new Mediator(services.Build());

			// Act
			await mediator.Send(new TestResponseCommand());

			// Assert
			Assert.Contains(logger.LogEntries, x =>
				x.Message.Contains("Handling request") &&
				x.Level == LogLevel.Information);
		}

		#endregion

		#region Void Command Tests

		[Fact]
		public async Task Send_VoidCommand_ShouldExecuteSuccessfully()
		{
			// Arrange
			var handler = new TestVoidCommandHandler();
			var services = new TestServiceProvider()
				.AddHandler(handler);

			var mediator = new Mediator(services.Build());

			// Act
			await mediator.Send(new TestVoidCommand());

			// Assert
			Assert.True(handler.WasExecuted);
		}

		[Fact]
		public async Task Send_VoidCommand_WithBehavior_ShouldExecutePipeline()
		{
			// Arrange
			var logger = new TestLogger<LoggingBehavior<RequestAdapter<TestVoidCommand>, Unit>>();
			var handler = new TestVoidCommandHandler();

			var services = new TestServiceProvider()
				.AddHandler(handler)
				.AddBehavior(new LoggingBehavior<RequestAdapter<TestVoidCommand>, Unit>(logger));

			var mediator = new Mediator(services.Build());

			// Act
			await mediator.Send(new TestVoidCommand());

			// Assert
			Assert.Contains(logger.LogEntries, x =>
				x.Message.Contains("Handling request") &&
				x.Level == LogLevel.Information);
		}

		#endregion

		#region Error Cases

		[Fact]
		public async Task Send_UnregisteredCommand_ShouldThrowException()
		{
			// Arrange
			var services = new TestServiceProvider();
			var mediator = new Mediator(services.Build());

			// Act & Assert
			await Assert.ThrowsAsync<InvalidOperationException>(
				() => mediator.Send(new TestResponseCommand()));
		}

		[Fact]
		public async Task Send_InvalidCommand_ShouldTriggerValidationBehavior()
		{
			// Arrange
			var handler = new TestResponseHandler(new TestResponse());
			var validator = new TestValidationBehavior<TestResponseCommand, TestResponse>(shouldFail: true);

			var services = new TestServiceProvider()
				.AddHandler<TestResponseCommand, TestResponse>(handler)
				.AddBehavior(validator);

			var mediator = new Mediator(services.Build());

			// Act & Assert
			await Assert.ThrowsAsync<ValidationException>(
				() => mediator.Send(new TestResponseCommand()));
		}

		#endregion

		#region Test Helpers

		private class TestServiceProvider
		{
			private readonly ServiceCollection _services = new();

			public TestServiceProvider()
			{
				// Register the adapter mapping
				_services.AddTransient(
					typeof(IRequestHandler<,>).MakeGenericType(typeof(RequestAdapter<>), typeof(Unit)),
					provider => {
						var requestType = typeof(RequestAdapter<>).GetGenericArguments()[0];
						var innerHandlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
						var innerHandler = provider.GetRequiredService(innerHandlerType);
						var adapterType = typeof(VoidCommandAdapter<>).MakeGenericType(requestType);
						return Activator.CreateInstance(adapterType, innerHandler)!;
					});
			}
			public TestServiceProvider AddHandler<TRequest>(IRequestHandler<TRequest> handler)
	   where TRequest : IRequest
			{
				// Register the concrete handler
				_services.AddSingleton(typeof(IRequestHandler<TRequest>), handler);

				// Explicitly register the adapter for this specific type
				_services.AddSingleton(
					typeof(IRequestHandler<RequestAdapter<TRequest>, Unit>),
					new VoidCommandAdapter<TRequest>(handler));

				return this;
			}
		

			public TestServiceProvider AddHandler<TRequest, TResponse>(IRequestHandler<TRequest, TResponse> handler)
				where TRequest : IRequest<TResponse>
			{
				_services.AddSingleton(handler);
				return this;
			}

			public TestServiceProvider AddBehavior<TRequest, TResponse>(IPipelineBehavior<TRequest, TResponse> behavior)
				where TRequest : IRequest<TResponse>
			{
				_services.AddSingleton(behavior);
				return this;
			}

			public IServiceProvider Build() => _services.BuildServiceProvider();
		}

		private class TestLogger<T> : ILogger<T>
		{
			public List<(LogLevel Level, string Message)> LogEntries { get; } = new();
			public IDisposable BeginScope<TState>(TState state) => new NullScope();
			public bool IsEnabled(LogLevel logLevel) => true;

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
				Exception exception, Func<TState, Exception, string> formatter)
			{
				LogEntries.Add((logLevel, formatter(state, exception)));
			}

			private class NullScope : IDisposable { public void Dispose() { } }
		}

		// Test Commands and Handlers
		private class TestVoidCommand : IRequest { }

		private class TestVoidCommandHandler : IRequestHandler<TestVoidCommand>
		{
			public bool WasExecuted { get; private set; }

			public Task Handle(TestVoidCommand request, CancellationToken cancellationToken)
			{
				WasExecuted = true;
				return Task.CompletedTask;
			}
		}

		private class TestResponseCommand : IRequest<TestResponse> { }

		private class TestResponse
		{
			public string? Value { get; set; }
		}

		private class TestResponseHandler : IRequestHandler<TestResponseCommand, TestResponse>
		{
			private readonly TestResponse _response;

			public TestResponseHandler(TestResponse response)
			{
				_response = response;
			}

			public Task<TestResponse> Handle(TestResponseCommand request, CancellationToken cancellationToken)
			{
				return Task.FromResult(_response);
			}
		}

		// Test Behaviors
		private class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		{
			private readonly ILogger _logger;

			public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
			{
				_logger = logger;
			}

			public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
			{
				_logger.LogInformation($"Handling request of type {typeof(TRequest).Name}");
				var response = await next(cancellationToken);
				_logger.LogInformation($"Handled request of type {typeof(TRequest).Name}");
				return response;
			}
		}


		private class TestValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		{
			private readonly bool _shouldFail;

			public TestValidationBehavior(bool shouldFail)
			{
				_shouldFail = shouldFail;
			}

			public Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken = default)
			{
				if (_shouldFail)
				{
					throw new ValidationException("Test validation failure");
				}
				return next(cancellationToken);
			}
		}

		private class ValidationException : Exception
		{
			public ValidationException(string message) : base(message) { }
		}

		#endregion
	}
}