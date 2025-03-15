using System.Text.Json.Serialization;

namespace FeatureManagementFilters.Models
{
	public record ProductPromotion
	{
		[JsonPropertyName("product_id")]
		public int ProductId { get; init; }
		[JsonPropertyName("product_name")]
		public string Name { get; init; }
		[JsonPropertyName("manufacturer_id")]
		public int ManufacturerId { get; init; }
		[JsonPropertyName("is_featured")]
		public bool IsFeatured { get; init; }
	}
}
