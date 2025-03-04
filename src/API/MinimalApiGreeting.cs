﻿using Asp.Versioning.Conventions;
using FeatureManagementFilters.Models;
using FeatureManagementFilters.Services.ProductService;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;


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
			api.MapPost("/minimal-custom-greeting", GetCustomGreeting);
			api.MapGet("/product-promotion", GetProductPromotion);
			api.MapGet("/product-recommendation", GetProductRocemmendation);

			return api;
		}
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

	}
}




