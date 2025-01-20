using Microsoft.AspNetCore.Mvc;

namespace FeatureManagementFilters.Models
{
	public class Greeting
	{
		[FromHeader]
		public required string Fullname { get; set; }
	}
}
