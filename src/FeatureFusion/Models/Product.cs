using System.ComponentModel.DataAnnotations;

namespace FeatureManagementFilters.Models
{
	public record Product:BaseEntity
	{
		public string Name { get; init; }
		public string FullDescription { get; set; }
		public bool Published { get; init; }
		public bool Deleted { get; init; }
		public bool VisibleIndividually { get; init; }
		public decimal Price { get; init ; }
	}
}
