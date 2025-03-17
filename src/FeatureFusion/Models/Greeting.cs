using Microsoft.AspNetCore.Mvc;

namespace FeatureFusion.Models
{
	public class Greeting
	{
		[FromHeader]
		public required string Fullname { get; set; }
	}
}
