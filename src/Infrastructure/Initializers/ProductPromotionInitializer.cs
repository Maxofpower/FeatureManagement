using FeatureManagementFilters.Services.ProductService;

namespace FeatureManagementFilters.Infrastructure.Initializers
{
	public class ProductPromotionInitializer : IAppInitializer
	{
		private readonly IServiceScopeFactory _serviceScopeFactory;
		public ProductPromotionInitializer(IServiceScopeFactory serviceScopeFactory)
		{
			_serviceScopeFactory = serviceScopeFactory;

		}
		public async Task InitializeAsync(CancellationToken cancellationToken = default)
		{
			using var scope = _serviceScopeFactory.CreateScope();
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductPromotionInitializer>>();
			var productService = scope.ServiceProvider.GetRequiredService<IProductService>();

			logger.LogInformation("==> Starting initialization of Product Promotions...");

			try
			{
				//  Read-through caching
				var promotions = await productService.GetProductPromotionAsync(cancellationToken);
				logger.LogInformation(" ==> Product Promotions initialized successfully.");
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Failed to initialize Product Promotions.");
				// Decide to fail or log and continue based on requirements
				throw;
			}

		}

	}
}
