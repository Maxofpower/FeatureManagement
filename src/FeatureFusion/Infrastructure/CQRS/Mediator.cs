

using FeatureFusion.Infrastructure.CQRS.Wrapper;
using System.Collections.Concurrent;
using FeatureFusion.Infrastructure.CQRS.Adapter;

namespace FeatureFusion.Infrastructure.CQRS
{
	public class Mediator : IMediator
	{

		public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);
		public delegate Task VoidRequestHandlerDelegate(CancellationToken cancellationToken);
		private readonly IServiceProvider _serviceProvider;
		private static readonly ConcurrentDictionary<Type, RequestHandlerWrapper> _requestHandlers = new();
		private static readonly ConcurrentDictionary<Type, PipelineBehaviorWrapper> _behaviorPipelines = new();

		public Mediator(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

		public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
		{
			if (request == null) throw new ArgumentNullException(nameof(request));

			var requestType = request.GetType();
			var handler = GetRequestHandler<TResponse>(requestType);
			var pipeline = GetBehaviorPipeline<TResponse>(requestType);

			return handler.Handle(request, pipeline, _serviceProvider, cancellationToken);
		}

		public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
		{
			if (request == null) throw new ArgumentNullException(nameof(request));

			var requestType = request.GetType();
			var adapterType = typeof(RequestAdapter<>).MakeGenericType(requestType);
			var adaptedRequest = (IRequest<Unit>)Activator.CreateInstance(adapterType, request)!;

			return Send(adaptedRequest, cancellationToken);
		}

		private RequestHandlerWrapper<TResponse> GetRequestHandler<TResponse>(Type requestType)
		{
			return (RequestHandlerWrapper<TResponse>)_requestHandlers.GetOrAdd(requestType, type =>
			{
				var wrapperType = typeof(RequestHandlerWrapper<,>).MakeGenericType(type, typeof(TResponse));
				return (RequestHandlerWrapper)Activator.CreateInstance(wrapperType)!;
			});
		}
		private PipelineBehaviorWrapper<TResponse> GetBehaviorPipeline<TResponse>(Type requestType)
		{
			return (PipelineBehaviorWrapper<TResponse>)_behaviorPipelines.GetOrAdd(requestType, type =>
			{
				var wrapperType = typeof(PipelineBehaviorWrapper<,>).MakeGenericType(type, typeof(TResponse));
				return (PipelineBehaviorWrapper)Activator.CreateInstance(wrapperType)!;
			});
		}

	
	}
}