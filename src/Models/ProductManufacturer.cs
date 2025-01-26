namespace FeatureManagementFilters.Models
{
	public record ProductManufacturer
	{
		public int ProductId { get; init; }
		public int ManufacturerId { get; init; }
		public bool IsFeaturedProduct { get; init; }
	}
}
