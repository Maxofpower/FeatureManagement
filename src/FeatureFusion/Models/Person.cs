using FeatureManagementFilters.Models;
using FeatureManagementFilters.Models.Validator;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System;
namespace FeatureFusion.Models
{
	public record Person : BaseEntity
	{
		public string Name { get; set; }
		public int Age { get; set; }
	};
	public class PersonValidator : AbstractValidator<Person>
	{
		public PersonValidator()
		{
			RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");
			RuleFor(x => x.Age).InclusiveBetween(18, 99).WithMessage("Age must be between 18 and 99.");
		}
		
	}

}
