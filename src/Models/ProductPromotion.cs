namespace FeatureManagementFilters.Models
{
	public record ProductPromotion
	{
		public int ProductId { get; init; }
		public string Name { get; init; }
		public int ManufacturerId { get; init; }
		public bool IsFeatured { get; init; }
	}
}
