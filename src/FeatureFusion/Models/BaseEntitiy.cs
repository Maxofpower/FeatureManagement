namespace FeatureManagementFilters.Models
{
	public abstract partial record BaseEntity
	{
		/// <summary>
		/// Gets or sets the entity identifier
		/// </summary>
		public int Id { get; set; }
	}
}
