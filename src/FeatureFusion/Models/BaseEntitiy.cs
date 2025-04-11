using System.ComponentModel.DataAnnotations;

namespace FeatureManagementFilters.Models
{
	public abstract partial record BaseEntity
	{
		[Key]
		public int Id { get; set; }
	}
}
