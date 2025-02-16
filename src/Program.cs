using Asp.Versioning.ApiExplorer;
using FeatureManagementFilters.API.V2;
using FeatureManagementFilters.Infrastructure;
using FeatureManagementFilters.Infrastructure.Exetnsion;
using FeatureManagementFilters.Infrastructure.Initializers;
using FeatureManagementFilters.Services.Authentication;
using FeatureManagementFilters.Services.FeatureToggleService;
using FeatureManagementFilters.Services.ProductService;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Use the generic method for JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddMemoryCache(); // This registers IMemoryCache


builder.Services.AddSingleton<IStaticCacheManager, MemoryCacheManager>();

// Use the generic method for feature management
builder.Services.AddFeatureManagementWithFilters<UseGreetingFilter>();

builder.Services.AddScoped<IAuthService, AuthService>();  // Registering the AuthService

builder.Services.AddScoped<IProductService, ProductService>();

builder.Services.AddScoped<IAppInitializer, ProductPromotionInitializer>();

builder.Services.AddScoped<IFeatureToggleService, FeatureToggleService>();


builder.Services.AddHostedService<AppInitializer>();


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

//To Present middleware dynamic caching example
// Resolve IFeatureManager to check the feature flag
var featureManager = app.Services.GetRequiredService<IFeatureManager>();

// Check if the feature flag is enabled
if (await featureManager.IsEnabledAsync("RecommendationCacheMiddleware"))
{
	// Add the middleware if the feature flag is enabled
	app.UseMiddleware<RecommendationCacheMiddleware>();
}


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

