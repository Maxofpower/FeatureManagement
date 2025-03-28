using System.Diagnostics;
using Microsoft.Extensions.Logging;
using static FeatureFusion.Infrastructure.CQRS.Mediator;

namespace FeatureFusion.Infrastructure.CQRS.Behaviors
{
	public class TelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
		where TRequest : IRequest<TResponse>
	{
		private readonly ILogger<TelemetryBehavior<TRequest, TResponse>> _logger;

		public TelemetryBehavior(ILogger<TelemetryBehavior<TRequest, TResponse>> logger)
		{
			_logger = logger;
		}

		public async Task<TResponse> Handle(TRequest request,
			RequestHandlerDelegate<TResponse> next, 
			CancellationToken cancellationToken = default)
		{
			var stopwatch = Stopwatch.StartNew();
			var activityName = $"{typeof(TRequest).Name} Handling";

			using var activity = new Activity(activityName);
			activity.SetTag("request.name", typeof(TRequest).Name);
			activity.SetTag("request.type", typeof(TRequest).FullName);
			activity.Start();

			try
			{
				_logger.LogInformation("Handling request {RequestType}", typeof(TRequest).Name);
				var response = await next(cancellationToken);
				stopwatch.Stop();

				_logger.LogInformation("Handled {RequestType} in {ElapsedMs} ms",
					typeof(TRequest).Name, stopwatch.ElapsedMilliseconds);

				activity.SetTag("request.success", true);
				activity.SetTag("request.duration_ms", stopwatch.ElapsedMilliseconds);

				return response;
			}
			catch (Exception ex)
			{
				stopwatch.Stop();
				_logger.LogError(ex, "Error handling {RequestType}: {ErrorMessage}", typeof(TRequest).Name, ex.Message);

				activity.SetTag("request.success", false);
				activity.SetTag("error.message", ex.Message);
				activity.SetTag("error.stacktrace", ex.StackTrace);
				activity.SetStatus(ActivityStatusCode.Error, ex.Message);

				throw;
			}
			finally
			{
				activity.Stop();
			}
		}
	}
}