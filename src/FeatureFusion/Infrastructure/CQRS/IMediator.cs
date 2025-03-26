namespace FeatureFusion.Infrastructure.CQRS
{
	public interface IMediator
	{
		Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

		Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
			where TRequest : IRequest;
	}

	public interface IRequest<out TResponse> { }

	public interface IRequest { }

	public interface IRequestHandler<in TRequest, TResponse> where TRequest : IRequest<TResponse>
	{
		Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
	}
	public interface IRequestHandler<in TRequest>
	where TRequest : IRequest
	{
		Task Handle(TRequest request, CancellationToken cancellationToken);
	}
	public interface IPipelineBehavior<in TRequest, TResponse>
	{
		Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken=default);
	}
	public interface IPipelineBehavior<TRequest>
	{
		Task Handle(TRequest request, RequestHandlerDelegate next, CancellationToken cancellationToken=default);
	}
	public delegate Task<TResponse> RequestHandlerDelegate<TResponse>();

	public delegate Task RequestHandlerDelegate();

}
