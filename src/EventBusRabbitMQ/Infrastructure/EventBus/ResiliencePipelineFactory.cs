
using EventBusRabbitMQ.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Fallback;
using Polly.Retry;
using Polly.Timeout;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Tasks;

public interface IResiliencePipelineProvider
{
	IAsyncPolicy<IConnection> GetConnectionPolicy();
	IAsyncPolicy<IModel> GetChannelPolicy();
	IAsyncPolicy GetPublishingPolicy();
	IAsyncPolicy GetConsumingPolicy();
}

public class ResiliencePipelineFactory : IResiliencePipelineProvider
{
	private readonly ResilienceOptions _options;
	private readonly ILogger<ResiliencePipelineFactory> _logger;
	private readonly ConcurrentDictionary<string, IAsyncPolicy> _nonGenericCache;
	private readonly ConcurrentDictionary<string, object> _genericCache;

	public ResiliencePipelineFactory(
		IOptions<ResilienceOptions> options,
		ILogger<ResiliencePipelineFactory> logger)
	{
		_options = options.Value;
		_logger = logger;
		_nonGenericCache = new ConcurrentDictionary<string, IAsyncPolicy>();
		_genericCache = new ConcurrentDictionary<string, object>();
	}

	public IAsyncPolicy<IConnection> GetConnectionPolicy() =>
		GetOrCreateGenericPolicy("connection", CreateConnectionPolicy);

	public IAsyncPolicy<IModel> GetChannelPolicy() =>
		GetOrCreateGenericPolicy("channel", CreateChannelPolicy);

	public IAsyncPolicy GetPublishingPolicy() =>
		_nonGenericCache.GetOrAdd("publish", _ => CreatePublishingPolicy());

	public IAsyncPolicy GetConsumingPolicy() =>
		_nonGenericCache.GetOrAdd("consume", _ => CreateConsumingPolicy());


	private IAsyncPolicy<T> GetOrCreateGenericPolicy<T>(string policyKey, Func<IAsyncPolicy<T>> policyFactory)
	{
		if (_genericCache.TryGetValue(policyKey, out var cachedPolicy) && cachedPolicy is IAsyncPolicy<T> typedPolicy)
			return typedPolicy;

		var newPolicy = policyFactory()
			.WithPolicyKey(policyKey);

		_genericCache[policyKey] = newPolicy;
		return newPolicy;
	}

	private IAsyncPolicy<IConnection> CreateConnectionPolicy()
	{
		return Policy<IConnection>
			.Handle<BrokerUnreachableException>()
			.Or<SocketException>()
			.Or<TimeoutException>()
			.Or<AlreadyClosedException>()
			.WaitAndRetryAsync(
				retryCount: _options.ConnectionRetryCount,
				sleepDurationProvider: attempt => CalculateBackoff(attempt),
				onRetry: (outcome, delay, retryCount, _) =>
				{
					_logger.LogWarning("Connection retry {RetryCount} after {DelayMs}ms",
						retryCount, delay.TotalMilliseconds);
				})
			.WrapAsync(Policy.TimeoutAsync<IConnection>(
				seconds: _options.ConnectionTimeoutSeconds,
				timeoutStrategy: TimeoutStrategy.Pessimistic));
	}

	private IAsyncPolicy<IModel> CreateChannelPolicy()
	{
		return Policy<IModel>
			.Handle<BrokerUnreachableException>()
			.Or<SocketException>()
			.Or<TimeoutException>()
			.Or<AlreadyClosedException>()
			.WaitAndRetryAsync(
				retryCount: _options.ChannelRetryCount,
				sleepDurationProvider: attempt => CalculateBackoff(attempt));
	}

	private IAsyncPolicy CreatePublishingPolicy()
	{
		return Policy
			.Handle<BrokerUnreachableException>()
			.Or<SocketException>()
			.Or<TimeoutException>()
			.WaitAndRetryAsync(
				retryCount: _options.PublishRetryCount,
				sleepDurationProvider: attempt => CalculateBackoff(attempt))
			.WrapAsync(Policy
				.Handle<Exception>()
				.CircuitBreakerAsync(
					exceptionsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
					durationOfBreak: TimeSpan.FromSeconds(_options.CircuitBreakerDuration)));
	}

	private IAsyncPolicy CreateConsumingPolicy()
	{
		return Policy
			.Handle<Exception>()
			.WaitAndRetryForeverAsync(
				sleepDurationProvider: attempt => CalculateBackoff(attempt));
	}

	private TimeSpan CalculateBackoff(int attempt)
	{
		var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, Math.Min(attempt, 8)));
		var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 500));
		return baseDelay + jitter;
	}
}

public class ResilienceOptions
{
	public int ConnectionRetryCount { get; set; } = 5;
	public int ConnectionTimeoutSeconds { get; set; } = 30;
	public int ChannelRetryCount { get; set; } = 3;
	public int PublishRetryCount { get; set; } = 3;
	public int CircuitBreakerThreshold { get; set; } = 10;
	public int CircuitBreakerDuration { get; set; } = 30;
}
