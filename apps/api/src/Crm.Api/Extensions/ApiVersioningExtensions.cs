using System.IdentityModel.Tokens.Jwt;
using Asp.Versioning;
using Crm.Api.OpenApi.Transformers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for registering and configuring API versioning services
/// in the dependency injection container.
/// </summary>
public static class ApiVersioningExtensions
{
    private const string ApiTitle = "CRM API";
    private const string ApiDescription = "REST API of the open-source CRM for non-governmental organizations.";

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
        .AddOpenApi(options =>
        {
            options.Document.AddDocumentTransformer((document, _, _) =>
            {
                document.Info.Title = ApiTitle;
                document.Info.Description = ApiDescription;

                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[JwtBearerDefaults.AuthenticationScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = JwtBearerDefaults.AuthenticationScheme,
                    BearerFormat = JwtConstants.TokenType,
                };

                return Task.CompletedTask;
            });

            options.Document.AddOperationTransformer<BearerSecurityOperationTransformer>();
            options.Document.AddOperationTransformer<AccessRightOperationTransformer>();
            options.Document.AddOperationTransformer<OperationIdOperationTransformer>();
            options.Document.AddOperationTransformer<JsonContentTypeOperationTransformer>();
            options.Document.AddOperationTransformer<JsonIgnoreParameterOperationTransformer>();
        });

        return services;
    }
}
