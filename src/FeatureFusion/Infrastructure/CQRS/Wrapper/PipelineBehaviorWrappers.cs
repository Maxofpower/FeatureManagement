using static FeatureFusion.Infrastructure.CQRS.Mediator;

namespace FeatureFusion.Infrastructure.CQRS.Wrapper
{	
	internal abstract class PipelineBehaviorWrapper { }
	internal abstract class PipelineBehaviorWrapper<TResponse> : PipelineBehaviorWrapper
	{
		public abstract Task<TResponse> Handle(
			IRequest<TResponse> request,
			RequestHandlerDelegate<TResponse> next,
			IServiceProvider serviceProvider,
			CancellationToken cancellationToken);
	}

	internal class PipelineBehaviorWrapper<TRequest, TResponse> : PipelineBehaviorWrapper<TResponse>
		where TRequest : IRequest<TResponse>
	{
		public override async Task<TResponse> Handle(
			IRequest<TResponse> request,
			RequestHandlerDelegate<TResponse> next,
			IServiceProvider serviceProvider,
			CancellationToken cancellationToken)
		{
			var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
			var pipeline = next;
			foreach (var behavior in behaviors.Reverse())
			{
				var current = pipeline;
				pipeline = ct => behavior.Handle((TRequest)request, current, ct);
			}
			return await pipeline(cancellationToken);
		}
	}

}

