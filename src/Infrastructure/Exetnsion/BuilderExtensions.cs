﻿using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.Conventions;
using FeatureManagementFilters.Infrastructure.Caching;
using FeatureManagementFilters.Infrastructure.Initializers;
using FeatureManagementFilters.Services.Authentication;
using FeatureManagementFilters.Services.FeatureToggleService;
using FeatureManagementFilters.Services.ProductService;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

namespace FeatureManagementFilters.Infrastructure.Exetnsion
{
	public static class ServiceConfigurationExtensions
	{
		// Generic method for configuring JWT Authentication
		public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
		{
			var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"] ?? string.Empty);
			if (key.Length < 32)
			{
				throw new ArgumentException("The key length must be at least 256 bits (32 bytes) long.");
			}

			services.AddAuthentication(options =>
			{
				options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
				options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
			})
			.AddJwtBearer(options =>
			{
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidateAudience = true,
					ValidateLifetime = true,
					ValidateIssuerSigningKey = true,
					ValidIssuer = configuration["Jwt:Issuer"],
					ValidAudience = configuration["Jwt:Audience"],
					IssuerSigningKey = new SymmetricSecurityKey(key)
				};
			});
		}

		// Generic method for configuring Feature Management with feature filters
		public static void AddFeatureManagementWithFilters<T>(this IServiceCollection services)
			where T : IFeatureFilterMetadata // Ensure T implements IFeatureFilterMetadata
		{
			services.AddFeatureManagement()
				.AddFeatureFilter<T>();
		}
		// Generic method for Swagger configuration
		public static void AddSwaggerConfiguration(this IServiceCollection services)
		{
			services.AddSwaggerGen(c =>
			{
				var provider = services.BuildServiceProvider().GetRequiredService<IApiVersionDescriptionProvider>();
				foreach (var description in provider.ApiVersionDescriptions)
				{
					c.SwaggerDoc(description.GroupName, new OpenApiInfo
					{
						Title = "API",
						Version = description.ApiVersion.ToString()
					});
				}

				// Add JWT Authentication to Swagger
				c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
				{
					Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
					Name = "Authorization",
					In = ParameterLocation.Header,
					Type = SecuritySchemeType.ApiKey,
					Scheme = "Bearer"
				});

				c.AddSecurityRequirement(new OpenApiSecurityRequirement
				{
					{
						new OpenApiSecurityScheme
						{
							Reference = new OpenApiReference
							{
								Type = ReferenceType.SecurityScheme,
								Id = "Bearer"
							},
							Scheme = "oauth2",
							Name = "Bearer",
							In = ParameterLocation.Header
						},
						new List<string>()
					}
				});
			});
		}

		// Generic method for API versioning
		public static void AddApiVersioningWithReader(this IServiceCollection services)
		{
			services.AddApiVersioning(options =>
			{
				//options.AssumeDefaultVersionWhenUnspecified = true;
				options.DefaultApiVersion = new ApiVersion(1, 0);
				options.ReportApiVersions = true;
				options.ApiVersionReader = ApiVersionReader.Combine(
					new QueryStringApiVersionReader("v"),
					new HeaderApiVersionReader("X-Version"),
					new UrlSegmentApiVersionReader()
				);
			})
			.AddApiExplorer(options =>
			{
				options.GroupNameFormat = "'v'V";
				options.SubstituteApiVersionInUrl = true;
			})
			.AddMvc(
				options =>
				{
					// automatically applies an api version namespace onventions
					options.Conventions.Add(new VersionByNamespaceConvention());
				});

		}

		public static void RegisterServices(this IServiceCollection services)
		{
			services.AddSingleton<IStaticCacheManager, MemoryCacheManager>();

			services.AddSingleton<IDistributedCacheManager, MemcachedCacheManager>();

			services.AddScoped<IAuthService, AuthService>();  

			services.AddScoped<IProductService, ProductService>();

			services.AddScoped<IAppInitializer, ProductPromotionInitializer>();

			services.AddScoped<IFeatureToggleService, FeatureToggleService>();

			services.AddHostedService<AppInitializer>();

		}
		public static class HealthCheckExtensions
		{
			public static Task WriteResponse(HttpContext context, HealthReport report)
			{
				context.Response.ContentType = "application/json; charset=utf-8";
				return context.Response.WriteAsync(JsonSerializer.Serialize(new
				{
					status = report.Status.ToString(),
					checks = report.Entries.Select(e => new
					{
						name = e.Key,
						status = e.Value.Status.ToString(),
						description = e.Value.Description
					}),
					duration = report.TotalDuration
				}));
			}
		}
	}
}

