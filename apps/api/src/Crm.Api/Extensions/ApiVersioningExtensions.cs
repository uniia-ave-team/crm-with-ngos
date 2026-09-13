using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for registering and configuring API versioning services
/// in the dependency injection container.
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Configures and registers API versioning services, setting up URL segment versioning,
    /// default versioning rules, and API explorer integration for documentation generation.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddCustomApiVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        })
        .AddOpenApi();

        services.ConfigureAll<OpenApiOptions>(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                var scheme = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = JwtConstants.TokenType,
                };

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = scheme;

                var requirement = new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme)] = [],
                };

                var operations = document.Paths?.Values.SelectMany(p => p.Operations.Values) ?? [];

                foreach (var operation in operations)
                {
                    operation.Security ??= [];
                    operation.Security.Add(requirement);
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }
}
