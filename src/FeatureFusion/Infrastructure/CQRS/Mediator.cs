using Enyim.Caching.Memcached.Protocol.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;

namespace FeatureFusion.Infrastructure.CQRS
{
	public class Mediator : IMediator
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ConcurrentDictionary<Type, object> _requestHandlers = new();

		public Mediator(IServiceProvider serviceProvider)
		{
			_serviceProvider = serviceProvider;
		}

		public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
		{
			var requestType = request.GetType();

			var handler = _requestHandlers.GetOrAdd(requestType, type =>
			{
				var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

				var handlerInstance = _serviceProvider.GetService(handlerType);

				if (handlerInstance == null)
				{
					throw new InvalidOperationException(
						$"Handler for request type {requestType.Name} with response {typeof(TResponse).Name} not registered");
				}

				return handlerInstance;
			});

			var pipelineBehaviors = _serviceProvider
				.GetServices(typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse)))
				.Cast<object>()
				.Reverse()
				.ToList();

			RequestHandlerDelegate<TResponse> next = () =>
			{
				var handleMethod = handler.GetType().GetMethod("Handle");
				var result = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, cancellationToken });
				return result;
			};

			// Apply behaviors in reverse order
			foreach (var behavior in pipelineBehaviors)
			{
				var current = next;
				var behaviorType = behavior.GetType();
				var handleMethod = behaviorType.GetMethod("Handle");

				next = () => (Task<TResponse>)handleMethod.Invoke(
					behavior,
					new object[] { request, current, cancellationToken });
			}

			return await next();
		}
		public async Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
	where TRequest : IRequest
		{
			if (request == null)
			{
				throw new ArgumentNullException(nameof(request));
			}

			var requestType = request.GetType();

			// Resolve pipeline behaviors for requests without response
			var pipelineBehaviors = _serviceProvider
				.GetServices<IPipelineBehavior<TRequest>>()
				.Reverse()
				.ToList();

			// Get or create the handler delegate for this request type
			var handler = _requestHandlers.GetOrAdd(requestType, type =>
			{
				var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
				var handlerInstance = _serviceProvider.GetService(handlerType);
				if (handlerInstance == null)
				{
					throw new InvalidOperationException(
						$"Handler for request type {requestType.Name} not registered");
				}
				return handlerInstance;
			});

			var handleMethod = handler.GetType().GetMethod("Handle");
			// Chain behaviors with the handler
			RequestHandlerDelegate next = () =>
			{
				var result = (Task)handleMethod.Invoke(handler, new object[] { request, cancellationToken });
				return result;
			};

			foreach (var behavior in pipelineBehaviors)
			{
				var current = next;
				next = () => behavior.Handle(request, current, cancellationToken);
			}

			// Execute the final pipeline
			await next();
		}
	}
}


