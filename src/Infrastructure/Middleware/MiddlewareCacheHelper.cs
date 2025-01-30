//using FeatureManagementFilters.Services.ProductService;

//namespace FeatureManagementFilters.Infrastructure.Middleware;
//public static class PersonalizedCacheHelper
//{
//	public static string GenerateCacheKey(HttpContext context)
//	{
//		var userId = context.User?.FindFirst("id")?.Value ?? "anonymous";
//		var region = context.Request.Headers["X-Region"].ToString();
//		var query = context.Request.QueryString.Value;
//		return $"user_{userId}_region_{region}_recommendations_{query}";
//	}

//	public static async Task<bool> TryServeFromCacheAsync(
//		HttpContext context,
//		IProductService cacheService,
//		string cacheKey)
//	{
//		var cachedResponse = await cacheService.GetProductPromotionAsync();
//		if (cachedResponse != null)
//		{
//			context.Response.ContentType = "application/json";
//			await context.Response.WriteAsync(cachedResponse);
//			return true;
//		}
//		return false;
//	}

//	public static async Task CacheResponseAsync(
//		HttpContext context,
//		IProductService cacheService,
//		string cacheKey,
//		TimeSpan expiration)
//	{
//		context.Response.Body.Seek(0, SeekOrigin.Begin);
//		var responseContent = await new StreamReader(context.Response.Body).ReadToEndAsync();
//		await cacheService.SetCacheAsync(cacheKey, responseContent, expiration);
//		context.Response.Body.Seek(0, SeekOrigin.Begin);
//	}
//}