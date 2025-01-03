using Asp.Versioning;
using FeatureManagementFilters.Models;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentValidation.Results;

namespace FeatureManagementFilters.Controllers.V2

{

	[ApiController]
	[Route("api/v{version:apiVersion}/[controller]")]
	
	public class GreetingController : ControllerBase
	{
		private readonly IFeatureManagerSnapshot _featureManager;
	

		public GreetingController(IFeatureManagerSnapshot featureManager)
		{
			
			_featureManager = featureManager;
		}

		[HttpPost("custom-greeting")]
		[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))] // Ok<string>
		[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))] // BadRequest<ValidationProblemDetails>
		[ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))] // NotFound<string>	
		public async Task<Results<Ok<string> , BadRequest<ValidationProblemDetails>, NotFound<string>>> GetCustomGreeting([AsParameters] Greeting greeting)
		{
			var _validator=new GreetingValidator();
			// Validate using the custom ValidateAsyncAndReturnProblem method
			var validationProblem = await _validator.ValidateAsyncAndReturnProblem(greeting);

			if (validationProblem != null)
			{
		
				return TypedResults.BadRequest(validationProblem);
			}

			
			if (await _featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return TypedResults.Ok($"Hello VIP user {greeting.Fullname}, this is your custom greeting V3!");
			}

			return TypedResults.Ok("Hello Anonymous user V3!");
		}
	}
}

