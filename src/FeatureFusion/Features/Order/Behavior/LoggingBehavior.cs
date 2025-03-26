namespace FeatureFusion.Features.Order.Behavior
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;
	using FeatureFusion.Infrastructure.CQRS;

	public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
	{
		private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

		public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		}

		public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
		{
			// Log the incoming request (before handling)
			_logger.LogInformation("Handling request of type {RequestType} with data: {RequestData}",
				typeof(TRequest).Name, request);

			// Call the next behavior/handler in the pipeline
			var response = await next();

			// Log the response (after handling)
			_logger.LogInformation("Handled request of type {RequestType} with response: {ResponseData}",
				typeof(TRequest).Name, response);

			return response;
		}
	}
}

