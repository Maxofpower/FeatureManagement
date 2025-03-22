using FeatureManagementFilters.Models.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace FeatureFusion.Models
{
	public class OrderRequest
	{
		public int ProductId { get; set; }
		public int Quantity { get; set; }
		public int CustomerId { get; set; }
	}
	public class OrderRequestValidator : BaseValidator<OrderRequest>
	{
		private readonly ILogger<OrderRequestValidator> _logger;

		public OrderRequestValidator(ILogger<OrderRequestValidator> logger)
		{
			_logger = logger;
			RuleFor(x => x.Quantity)
		   .GreaterThan(0).WithMessage("Quantity must be greater than 0");
		}
		public async Task<ValidationResult> ValidateWithResultAsync(OrderRequest item)
		{
			var validationResult = await ValidateAsync(item);

			if (!validationResult.IsValid)
			{
				var validationErrors = validationResult.Errors
					.GroupBy(e => e.PropertyName)
					.ToDictionary(
						group => group.Key,
						group => group.Select(e => e.ErrorMessage).ToArray()
					);

				_logger.LogError($"validation error on {nameof(Greeting)}: {validationErrors}");

				var problemDetails = new ValidationProblemDetails
				{
					Status = StatusCodes.Status400BadRequest,
					Title = "One or more validation errors occurred.",
					Errors = validationErrors
				};

				return ValidationResult.Failure(problemDetails);
			}

			return ValidationResult.Success();
		}



	}

}
