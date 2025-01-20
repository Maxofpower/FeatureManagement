using Asp.Versioning.ApiExplorer;
using FeatureManagementFilters.Extensions;
using FeatureManagementFilters.Services;
using FeatureManagementFilters.Validator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using FluentValidation;
using FluentValidation.AspNetCore;
using FeatureManagementFilters.Models;
using Asp.Versioning;
using Asp.Versioning.Builder;
using Asp.Versioning.Conventions;
using FeatureManagementFilters.API;
using FeatureManagementFilters.API.V2;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Use the generic method for JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Use the generic method for feature management
builder.Services.AddFeatureManagementWithFilters<UseGreetingFilter>();

builder.Services.AddScoped<IAuthService, AuthService>();  // Registering the AuthService

// Use the generic method for API versioning
builder.Services.AddApiVersioningWithReader();

// Add controllers and other necessary services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor(); // Register IHttpContextAccessor


//Fluent Validator
builder.Services.AddFluentValidationAutoValidation()
				.AddValidatorsFromAssemblyContaining<Program>();
// Add Swagger configuration
builder.Services.AddSwaggerConfiguration();

var app = builder.Build();

// Configure middleware and endpoints
ConfigureSwaggerUI(app);
ConfigureRequestPipeline(app);


#region Middleware Configuration

void ConfigureSwaggerUI(WebApplication app)
{
	app.UseSwagger();
	app.UseSwaggerUI(options =>
	{
		var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();
		foreach (var description in provider.ApiVersionDescriptions)
		{
			options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
		}
	});
}

void ConfigureRequestPipeline(WebApplication app)
{
	if (app.Environment.IsDevelopment())
	{
		app.UseDeveloperExceptionPage();
	}

	app.UseHttpsRedirection();
	app.UseAuthorization();

	// Map API controllers and versioned routes
	app.MapControllers();
}
#endregion


app.MapGreetingApiV2();

app.Run();

public partial class Program { }

