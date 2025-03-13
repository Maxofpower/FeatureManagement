using FeatureManagementFilters.Infrastructure.Caching;

public interface IDistributedCacheManager
{
	Task<T> GetValueOrCreateAsync<T>(CacheKey key, Func<Task<T>> acquire);
	Task RefreshCacheAsync<T>(string key, Func<Task<T>> fetchFromDb, int cacheMinutes);
}