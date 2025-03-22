using Asp.Versioning.ApiExplorer;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using FeatureFusion.Infrastructure.Exetnsion;
using FeatureFusion.Infrastructure.ValidationProvider;
using FeatureFusion.Models;
using FeatureManagementFilters.API.V2;
using FeatureManagementFilters.Infrastructure.Caching;
using FeatureManagementFilters.Infrastructure.Initializers;
using FeatureManagementFilters.Services.Authentication;
using FeatureManagementFilters.Services.FeatureToggleService;
using FeatureManagementFilters.Services.ProductService;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.FeatureManagement;
using System.ComponentModel.DataAnnotations;
using static RedisSettings;

var builder = WebApplication.CreateBuilder(args);

// Use the generic method for JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddMemoryCache();

builder.Services.AddFeatureManagementWithFilters<UseGreetingFilter>();

builder.Services.RegisterServices();

// Register all validators in the current assembly
//builder.Services.AddAllValidators();

builder.Services.AddApiVersioningWithReader();

// Add controllers and other necessary services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor(); // Register IHttpContextAccessor



// Add Swagger configuration
builder.Services.AddSwaggerConfiguration();
// Resolve IFeatureManager to check the feature flag

#region Cache Provider

var memcachedSection = builder.Configuration.GetSection("Memcached");

builder.Services
	.AddOptions<MemcachedClientOptions>()
	.Bind(memcachedSection)
	.ValidateDataAnnotations()
	.Validate(options => options.Servers?.Any() ?? false, "At least one Memcached server must be configured")
	.ValidateOnStart();

builder.Services.AddEnyimMemcached();

// Bind and validate Redis configuration
var redisSection = builder.Configuration.GetSection("Redis");
builder.Services
	.AddOptions<RedisOptions>()
	.Bind(redisSection)
	.ValidateDataAnnotations()
	.Validate(options => !string.IsNullOrEmpty(options.ConnectionString), "Redis connection string is required.")
	.ValidateOnStart();


builder.Services.AddCacheWithRedis(builder.Configuration);

#endregion

var app = builder.Build();
var featureManager = app.Services.GetRequiredService<IFeatureManager>();
//To Present middleware dynamic caching example
var useMemcached = await featureManager.IsEnabledAsync("MemCachedEnabled");
var  useRedis = await featureManager.IsEnabledAsync("IdempotencyEnabled");

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

#region memchached prestart up validation if enabled
// Pre-startup validation for memcached and redis
if (useMemcached)
{
	using var scope = app.Services.CreateScope();
	var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
	var memcachedClient = scope.ServiceProvider.GetRequiredService<IMemcachedClient>();

	try
	{
		logger.LogInformation("Validating Memcached connection...");

		// Test write/read operation
		var testKey = $"startup_test_{Guid.NewGuid()}";
		var testValue = "connection_test_value";
		var expiration = TimeSpan.FromSeconds(5);

		// Write test value
		var setSuccess = await memcachedClient.SetAsync(testKey, testValue, expiration);
		if (!setSuccess)
		{
			throw new InvalidOperationException("Failed to write test value to Memcached");
		}

		// Read test value
		var retrievedValue = await memcachedClient.GetAsync<string>(testKey);
		if (retrievedValue?.Value != testValue)
		{
			throw new InvalidOperationException("Failed to read test value from Memcached");
		}

		logger.LogInformation("Memcached connection validated successfully");
	}
	catch (Exception ex)
	{
		logger.LogCritical(ex, "Memcached pre-startup validation failed");
		throw; // Prevent application startup
	}
}
if(useRedis)
{
	using var scope = app.Services.CreateScope();
	var redis = scope.ServiceProvider.GetRequiredService<IDistributedCache>();
	try
	{
		await redis.GetAsync("tese");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[Redis startup Error] {ex.Message}");
		throw;
	}
}

#endregion

	app.Run();

public partial class Program { }

