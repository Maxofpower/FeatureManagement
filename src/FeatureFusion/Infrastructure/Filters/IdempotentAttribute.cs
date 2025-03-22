namespace FeatureFusion.Infrastructure.Filters
{
	using FeatureFusion.Infrastructure.Caching;
	using Microsoft.AspNetCore.Mvc.Filters;
	using Microsoft.Extensions.Caching.Distributed;
	using Microsoft.Extensions.Caching.Hybrid;

	[AttributeUsage(AttributeTargets.Method)]
	public class IdempotentAttribute : Attribute, IFilterFactory
	{
		public bool UseLock { get; set; }

		public IdempotentAttribute(bool useLock = false)
		{
			UseLock = useLock;
		}
		public bool IsReusable => false; // Filters are not reusable

		public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
		{
			var distributedCache = serviceProvider.GetService<IDistributedCache>();
			var redisWrapper = serviceProvider.GetService<IRedisConnectionWrapper>();
			var loggerFactory = (ILoggerFactory)serviceProvider.GetService(typeof(ILoggerFactory));

			return new IdempotentAttributeFilter(
				distributedCache,
				loggerFactory,
				redisWrapper,
				useLock: UseLock);		
		}
	}
}
