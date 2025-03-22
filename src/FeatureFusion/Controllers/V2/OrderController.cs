using FeatureFusion.Infrastructure.Filters;
using FeatureFusion.Models;
using FeatureManagementFilters.Models;
using FeatureManagementFilters.Services.FeatureToggleService;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFusion.Controllers.V2
{
	[ApiController]
	[Route("api/v{version:apiVersion}/[controller]")]
	public class OrderController : Controller
	{
		private readonly OrderRequestValidator _validator;
		public OrderController(OrderRequestValidator validator)
		{
			_validator = validator;
		}

		// to test idempotent-filter
		[HttpPost("order")]
		[ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderResponse))] // Ok<OrderResponse>
		[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))] // BadRequest<ValidationProblemDetails>
		[ProducesResponseType(StatusCodes.Status404NotFound, Type = typeof(string))] // NotFound<string>
		[Idempotent(useLock: true)] // Apply the Idempotent attribute
		public async Task<ActionResult<OrderResponse>> CreateOrder([FromBody] OrderRequest request)
		{
			// Validate the request
			var validationResult = await _validator.ValidateWithResultAsync(request);

			if (!validationResult.IsValid)
			{
				// Return a BadRequest if validation fails
				return BadRequest(validationResult.ProblemDetails);
			}

			// Simulate fetching product details (in-memory data)
			var product = new Product
			{
				Id = 12345,
				Name = "Smartphone",
				Price = 599.99m
			};

			// Simulate fetching customer details (in-memory data)
			var customer = new Person
			{
				Id = 11111,
				Name = "John Doe"
			};

			// Simulate order creation (it has to get cached if it's an idempotent request)
			var orderId = Ulid.NewUlid();
			var orderTotal = product.Price * request.Quantity;

			// Build the response
			var response = new OrderResponse
			{
				OrderId = orderId,
				CustomerName = customer.Name,
				ProductName = product.Name,
				Quantity = request.Quantity,
				TotalAmount = orderTotal,
				OrderDate = DateTime.UtcNow,
				Message = "Order created successfully."
			};

			// Return an OK response with the order response
			return Ok(response);
		}

		public class OrderResponse
		{
			public Ulid OrderId { get; set; }
			public string CustomerName { get; set; }
			public string ProductName { get; set; }
			public int Quantity { get; set; }
			public decimal TotalAmount { get; set; }
			public DateTime OrderDate { get; set; }
			public string Message { get; set; }
		}
	}
}


