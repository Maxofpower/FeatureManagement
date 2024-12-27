using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using System.Threading.Tasks;

namespace FeatureManagementFilters.Controllers.V2

{
	//[ApiVersion("2.0")]
	[ApiController]
	[Route("api/v{version:apiVersion}/[controller]")]
	
	public class GreetingController : ControllerBase
	{
		private readonly IFeatureManagerSnapshot _featureManager;

		public GreetingController(IFeatureManagerSnapshot featureManager)
		{
			_featureManager = featureManager;
		}
	//	[MapToApiVersion("2.0")]
		[HttpGet("custom-greeting")]
		public async Task<IActionResult> GetCustomGreeting()
		{
			if (await _featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return Ok("Hello VIP user, this is your custom greeting V2!");
			}

			return Ok("Hello Anonymous user V2!");
		}
	}
}