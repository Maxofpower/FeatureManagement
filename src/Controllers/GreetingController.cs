using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using System.Threading.Tasks;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public class GreetingController : ControllerBase
{
	private readonly IFeatureManagerSnapshot _featureManager;

	public GreetingController(IFeatureManagerSnapshot featureManager)
	{
		_featureManager = featureManager;
	}

	[HttpGet("custom-greeting")]
	public async Task<IActionResult> GetCustomGreeting()
	{
		if (await _featureManager.IsEnabledAsync("CustomGreeting"))
		{
			return Ok("Hello VIP user, this is your custom greeting!");
		}

		return Ok("Hello Anonymous user!");
	}
}
