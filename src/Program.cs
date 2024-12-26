using FeatureManagementFilters.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;

var builder = WebApplication.CreateBuilder(args);

// Use the generic method for JWT Authentication
builder.Services.AddJwtAuthentication(builder.Configuration);

// Use the generic method for feature management
builder.Services.AddFeatureManagementWithFilters<UseGreetingFilter>();

// Use the generic method for API versioning
builder.Services.AddApiVersioningWithReader();

// Add controllers and other necessary services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpContextAccessor(); // Register IHttpContextAccessor

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
	app.UseSwaggerUI(c =>
	{
		c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
		//c.RoutePrefix = string.Empty; // Optional: Serve the Swagger UI at the root URL
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

app.Run();

public partial class Program { }

