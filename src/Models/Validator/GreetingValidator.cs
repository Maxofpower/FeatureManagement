using FeatureManagementFilters.Models;
using FeatureManagementFilters.Models.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;


public class GreetingValidator : BaseValidator<Greeting>
{
	public readonly ILogger<GreetingValidator> _logger;
	public ILogger<GreetingValidator> Logger => _logger;

	public GreetingValidator(ILogger<GreetingValidator> logger)
	{
		_logger = logger;
		RuleFor(x => x.Fullname)
	   .NotEmpty().WithMessage("FullName is required.")
	   .NotNull().WithMessage("FullName is required.")
	   .Length(1, 100).WithMessage("FullName must be between 1 and 100 characters.");

	}

	public async Task<ValidationResult> ValidatWithResultAsync(Greeting item)
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
