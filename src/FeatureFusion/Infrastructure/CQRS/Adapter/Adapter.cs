namespace FeatureFusion.Infrastructure.CQRS.Adapter
{
	public class VoidCommandAdapter<TRequest> : IRequestHandler<RequestAdapter<TRequest>, Unit>
	where TRequest : IRequest
	{
		private readonly IRequestHandler<TRequest> _innerHandler;

		public VoidCommandAdapter(IRequestHandler<TRequest> innerHandler)
		{
			_innerHandler = innerHandler;
		}

		public async Task<Unit> Handle(RequestAdapter<TRequest> request, CancellationToken cancellationToken)
		{
			await _innerHandler.Handle(request.Request, cancellationToken);
			return Unit.Value;
		}
	}

	public class RequestAdapter<TRequest> : IRequest<Unit>
	where TRequest : IRequest
	{
		public TRequest Request { get; }
		public RequestAdapter(TRequest request) => Request = request;
	}
}
