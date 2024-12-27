using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Asp.Versioning.Conventions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace FeatureManagementFilters.Extensions
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

		// Generic method to configure Feature Management
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
					c.SwaggerDoc(description.GroupName, new Microsoft.OpenApi.Models.OpenApiInfo
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
				options.AssumeDefaultVersionWhenUnspecified = true;
				options.DefaultApiVersion = new ApiVersion(1, 0);
				options.ReportApiVersions = true;
				options.ApiVersionReader = ApiVersionReader.Combine(
					new QueryStringApiVersionReader("v"),
					new HeaderApiVersionReader("X-Version"),
					new UrlSegmentApiVersionReader()
				);
			}).AddMvc(
				options =>
				{
					// automatically applies an api version namespace onventions
					options.Conventions.Add(new VersionByNamespaceConvention());
				})
				.AddApiExplorer(options =>
			{
				options.GroupNameFormat = "'v'V";
				options.SubstituteApiVersionInUrl = true;
			});
		}
	}
}

