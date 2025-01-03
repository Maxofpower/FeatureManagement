using FeatureManagementFilters.Models;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

public class GreetingValidator : AbstractValidator<Greeting>
{
	public GreetingValidator()
	{
		RuleFor(x => x.Fullname)
			.NotEmpty().WithMessage("FullName is required.")
			.NotNull().WithMessage("FullName is required.")
			.Length(1, 100).WithMessage("FullName must be between 1 and 100 characters.");
	}

	public async Task<ValidationProblemDetails?> ValidateAsyncAndReturnProblem(Greeting item)
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

			return new ValidationProblemDetails
			{
				Status = StatusCodes.Status400BadRequest,
				Title = "One or more validation errors occurred.",
				Errors = validationErrors
			};
		}

		// Return null if validation is successful
		return null;
	}
}
