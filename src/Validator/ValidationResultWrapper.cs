using Microsoft.AspNetCore.Mvc;

public class ValidationResult
{
	public bool IsValid { get; set; }
	public ValidationProblemDetails ProblemDetails { get; set; }

	public static ValidationResult Success() =>
		new ValidationResult { IsValid = true };

	public static ValidationResult Failure(ValidationProblemDetails problemDetails) =>
		new ValidationResult { IsValid = false, ProblemDetails = problemDetails };
}