using FeatureFusion.Features.Order.Commands;
using FeatureFusion.Infrastructure.Filters;
using FeatureFusion.Infrastructure.CQRS;
using FeatureFusion.Models;
using FeatureManagementFilters.Models;
using FeatureManagementFilters.Services.FeatureToggleService;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static FeatureFusion.Features.Order.Commands.CreateOrderCommandHandler;


namespace FeatureFusion.Controllers.V2
{
	[ApiController]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class OrderController : Controller
	{
		private readonly OrderRequestValidator _validator;
		private readonly IMediator _mediator;
		public OrderController(OrderRequestValidator validator,IMediator mediator)
		{
			_validator = validator;
			_mediator = mediator;
		}

		// to test idempotent-filter
		[HttpPost("order")]
		[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderResponse))] // Ok<OrderResponse>
		[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))] // BadRequest<ValidationProblemDetails>
		[ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))] // NotFound<string>
		[Idempotent(useLock: true)] // Apply the Idempotent attribute
		public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] CreateOrderCommand request)
		{
			// Validate the request
			var validationResult = await _validator.ValidateWithResultAsync(request);

			if (!validationResult.IsValid)
			{
				return BadRequest(validationResult.ProblemDetails);
			}
		
			var createOrderResult= await _mediator.Send(request);
			
			return Ok(createOrderResult);
		}

	}
}


