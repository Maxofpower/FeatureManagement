using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Enyim.Caching;
using FeatureManagementFilters.Infrastructure.Caching;
using Microsoft.Extensions.Logging;


public class MemcachedCacheManager : IDistributedCacheManager
{

		private readonly IMemcachedClient _memcachedClient;
		private readonly ILogger<MemcachedCacheManager> _logger;
		private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
		public MemcachedCacheManager(IMemcachedClient memcachedClient, ILogger<MemcachedCacheManager> logger)
		{
			_memcachedClient = memcachedClient;
			_logger = logger;
		}
		public async Task<T> GetAsync<T>(CacheKey key, Func<Task<T>> acquire)
		{
			try
			{
				// 🚀 Use GetValueOrCreateAsync to handle cache misses and data fetching in one call
				// 🛡️ Prevents cache stampedes: Ensures only one thread fetches data for a given key
				// 🛡️ Handles race conditions: Internal locking ensures thread-safe cache updates
				var cacheEntry = await _memcachedClient.GetValueOrCreateAsync(
					key.Key, // Cache key
					key.CacheTimeSecond, // Cache expiration time
					async () => await acquire() // Factory method to fetch data if cache miss

				);
			_logger.LogInformation("cacheEntry for key {key.key} is {cacheEntry}:",key.Key, cacheEntry);
				return cacheEntry;
			}
			catch (Exception ex)
			{
			// 🛑 Log errors and fallback to fetching fresh data
			_logger.LogError("Memcached Error {ex.Message}", ex.Message);
				return await acquire(); // 🛠️ Fallback to DB
			}
		}
	    public async Task RefreshCacheAsync<T>(string key, Func<Task<T>> fetchFromDb, int cacheMinutes)
		{
			try
			{
				var freshData = await fetchFromDb();
				await _memcachedClient.SetAsync(key, freshData, TimeSpan.FromMinutes(cacheMinutes));
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Memcached Refresh Error] {ex.Message}");
			}
		}

	}

