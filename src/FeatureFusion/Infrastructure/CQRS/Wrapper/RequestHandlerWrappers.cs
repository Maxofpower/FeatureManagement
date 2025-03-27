
using FeatureFusion.Infrastructure.CQRS.Adapter;
using static FeatureFusion.Infrastructure.CQRS.Mediator;
namespace FeatureFusion.Infrastructure.CQRS.Wrapper
{

	internal abstract class RequestHandlerWrapper { }
	internal abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapper
	{
		public abstract Task<TResponse> Handle(
			IRequest<TResponse> request,
			PipelineBehaviorWrapper<TResponse> pipeline,
			IServiceProvider serviceProvider,
			CancellationToken cancellationToken);
	}

	internal class RequestHandlerWrapper<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
		where TRequest : IRequest<TResponse>
	{
		public override Task<TResponse> Handle(
	 IRequest<TResponse> request,
	 PipelineBehaviorWrapper<TResponse> pipeline,
	 IServiceProvider serviceProvider,
	 CancellationToken cancellationToken)
		{

			// Normal request handling
			var normalhandler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
			return pipeline.Handle(
				request,
				ct => normalhandler.Handle((TRequest)request, ct),
				serviceProvider,
				cancellationToken);
		}

	}
}
