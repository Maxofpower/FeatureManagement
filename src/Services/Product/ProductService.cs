using FeatureManagementFilters.Infrastructure;
using FeatureManagementFilters.Models;
namespace FeatureManagementFilters.Services.ProductService
{
	public interface IProductService
	{
		Task<IList<ProductPromotion>> GetProductPromotionAsync(CancellationToken cancellationToken = default);
	}

	public class ProductService : IProductService
	{
		protected readonly IStaticCacheManager _staticCacheManager;

		public ProductService(IStaticCacheManager staticCacheManager)
		{
			_staticCacheManager = staticCacheManager;
		}

		public async Task<IList<ProductPromotion>> GetProductPromotionAsync(CancellationToken cancellationToken = default)
		{
			try
			{
				var cacheKey = new CacheKey("Promotion.BlackFriday");
				//	Console.WriteLine("==> Trying to get data from the cache for key: Promotion.BlackFriday...");

				// Check if cancellation is requested before starting any operation
				cancellationToken.ThrowIfCancellationRequested();

				var productPromotion = await _staticCacheManager.GetAsync(cacheKey, () =>
				{
					//	Console.WriteLine("==> Cache miss for key: Promotion.BlackFriday. Fetching data from the source...");
					// Static data representing products and their manufacturer promotions
					var products = new List<Product>
					{
					new Product { Id = 1, Name = "Laptop", Published = true, Deleted = false, VisibleIndividually = true },
					new Product { Id = 2, Name = "Phone", Published = true, Deleted = false, VisibleIndividually = true },
					new Product { Id = 3, Name = "Headphones", Published = false, Deleted = false, VisibleIndividually = true }
					};

					var productManufacturers = new List<ProductManufacturer>
					{
					new ProductManufacturer { ProductId = 1, ManufacturerId = 10, IsFeaturedProduct = true },
					new ProductManufacturer { ProductId = 2, ManufacturerId = 20, IsFeaturedProduct = true },
					new ProductManufacturer { ProductId = 3, ManufacturerId = 10, IsFeaturedProduct = false }
					};

					// Filtering and projecting product promotions based on the static data
					var query = from p in products
								join pm in productManufacturers on p.Id equals pm.ProductId
								where p.Published && !p.Deleted && p.VisibleIndividually &&
									  pm.IsFeaturedProduct
								select new ProductPromotion
								{
									ProductId = p.Id,
									Name = p.Name,
									ManufacturerId = pm.ManufacturerId,
									IsFeatured = pm.IsFeaturedProduct
								};

					return Task.FromResult(query.ToList()); // Return the filtered data
				});

				// i return result here for debug purpose , for production appInitilizer there is no need to return data
				return productPromotion;
			}
			catch
			{
				throw;
			}
		}
	}
}

