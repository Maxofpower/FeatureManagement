using FeatureManagementFilters.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

namespace FeatureManagementFilters.Controllers.V2

{

	[ApiController]
	[Route("api/v{version:apiVersion}/[controller]")]

	public class GreetingController : ControllerBase
	{
		private readonly IFeatureManagerSnapshot _featureManager;
		private readonly GreetingValidator _validator;


		public GreetingController(IFeatureManagerSnapshot featureManager, GreetingValidator validator)
		{

			_featureManager = featureManager;
			_validator = validator;
		}

		[HttpPost("custom-greeting")]
		[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))] // Ok<string>
		[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))] // BadRequest<ValidationProblemDetails>
		[ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))] // NotFound<string>	
		public async Task<Results<Ok<string>, BadRequest<ValidationProblemDetails>, NotFound<string>>> GetCustomGreeting(Greeting greeting)
		{

			var validationResult = await _validator.ValidatWithResultAsync(greeting);

			if (!validationResult.IsValid)
			{

				return TypedResults.BadRequest(validationResult.ProblemDetails);
			}


			if (await _featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return TypedResults.Ok($"Hello VIP user {greeting.Fullname}, this is your custom greeting V2!");
			}

			return TypedResults.Ok("Hello Anonymous user V2!");
		}
	}
}

