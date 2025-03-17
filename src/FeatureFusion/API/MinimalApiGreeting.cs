using Asp.Versioning.Conventions;
using FeatureFusion.Infrastructure.Exetnsion;
using FeatureFusion.Models;
using FeatureManagementFilters.Models;
using FeatureManagementFilters.Services.ProductService;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using System;


namespace FeatureManagementFilters.API.V2
{
	public static class FeatureMinimalsApi
	{
		public static RouteGroupBuilder MapGreetingApiV2(this IEndpointRouteBuilder app)
		{
			// Create a version set
			var apiVersionSet = app.NewApiVersionSet()
								   .HasApiVersion(2.0)
								   .ReportApiVersions()
								   .Build();

			var api = app.MapGroup("api/v{version:apiVersion}")
			 .WithApiVersionSet(apiVersionSet)
			 .MapToApiVersion(2.0);
		
			api.MapGet("/product-promotion", GetProductPromotion);
			api.MapGet("/product-recommendation", GetProductRocemmendation);

			// to present manual Validation handling with dipendency injection
			api.MapPost("/minimal-custom-greeting", GetCustomGreeting)
				.Produces<Ok<string>>()
				.ProducesValidationProblem()
				.Produces<NotFound<string>>();


			// Approach 1: Using a generic endpoint filter with `AddEndpointFilter`
			// This approach applies validation by adding a generic endpoint filter to the route.
			api.MapPost("/person-endpointfilter", HandleCreatePerson)
			   .AddEndpointFilter<ValidationFilter<Person>>();

			// Approach 2: Using a generic endpoint extension method with `WithValidation`
			// This approach applies validation fluently using a custom extension method.
			api.MapPost("/person-builderextension", HandleCreatePerson)
			   .WithValidation<Person>();

			// Approach 3: Using a custom route handler builder extension
			// This approach encapsulates the endpoint definition and validation in a single method.
			api.MapPostWithValidation<Person>("/person-genericendpoint", HandleCreatePerson);
			

			return api;
		}
		
		public static async Task<Results<Ok<IList<ProductPromotion>>, NotFound<string>>> GetProductPromotion(
	         IProductService productService,  
			 bool getFromMemCach = false)
		{
			try
			{
				var promotions = await productService.GetProductPromotionAsync(getFromMemCach);

				// Return TypedResults.Ok with a list of ProductPromotion
				return TypedResults.Ok(promotions);
			}
			catch
			{
				// Return TypedResults.NotFound with an error message
				return TypedResults.NotFound("An error occurred while fetching promotions.");
			}
		}

		public static async Task<Results<Ok<List<ProductPromotion>>, NotFound<string>>> GetProductRocemmendation(
	 IProductService productService)
		{
			try
			{
				var recommedation = await productService.GetProductRocemmendationAsync();

				// Return TypedResults.Ok with a list of ProductPromotion
				return TypedResults.Ok(recommedation);
			}
			catch
			{
				// Return TypedResults.NotFound with an error message
				return TypedResults.NotFound("An error occurred while fetching promotions.");
			}
		}

		// 1- to present manual Validation handling with dipendency injection
		public static async Task<Results<Ok<string>, BadRequest<ValidationProblemDetails>, NotFound<string>>> GetCustomGreeting
			([AsParameters] Greeting greeting,
			GreetingValidator validator,
			IFeatureManager featureManager,
			ILogger<GreetingValidator> _logger)
		{
			// Use the validator to validate the incoming model
			var validationResult = await validator.ValidateWithResultAsync(greeting);

			if (!validationResult.IsValid)
			{
				_logger.LogWarning("validation error on {GreetingType}: {Errors}",
					nameof(Greeting), validationResult.ProblemDetails!.Errors);

				// Return problem details if validation fails
				return TypedResults.BadRequest(validationResult.ProblemDetails);

			}

			if (await featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return TypedResults.Ok($"Hello VIP user {greeting.Fullname}, this is your custom greeting V2!");
			}

			return TypedResults.Ok("Hello Anonymous user!");
		}
	

		// 2- to present dynamic Validation with generic endpoint filter
		public static async Task<Results<Ok<string>, BadRequest<ValidationProblemDetails>, NotFound<string>>> HandleCreatePerson
		([AsParameters] Person person,
		IFeatureManager featureManager)
		{

			if (await featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return TypedResults.Ok($"Hello VIP person {person.Name}, this is your custom greeting V2!");
			}

			return TypedResults.Ok($"Hello Guest person! {person.Name}");
		}

		// 3- to present dynamic Validation with generic ModelBinder
		public static async Task<Results<Ok<string>, BadRequest<ValidationProblemDetails>, NotFound<string>>> HandleCreatePerson2
			([AsParameters] Person person,
		IFeatureManager featureManager)
		{

			if (await featureManager.IsEnabledAsync("CustomGreeting"))
			{
				return TypedResults.Ok($"Hello VIP person {person.Name}, this is your custom greeting V2!");
			}

			return TypedResults.Ok($"Hello Guest person! {person.Name}");
		}

	}
}




