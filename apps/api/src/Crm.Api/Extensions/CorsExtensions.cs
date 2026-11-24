using Crm.Api.Consts;
using Crm.Api.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring Cross-Origin Resource Sharing (CORS) policies in the API layer.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Registers and validates <see cref="AppCorsOptions"/> and configures the application's CORS policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="environment">The web hosting environment.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCustomCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.AddOptions<AppCorsOptions>()
            .Bind(configuration.GetSection(AppCorsOptions.Position))
            .ValidateDataAnnotations()
            .Validate(
                options => environment.IsDevelopment() || options.AllowedOrigins is { Length: > 0 },
                "CORS allowed origins are not configured.")
            .ValidateOnStart();

        services.AddCors();

        services.AddOptions<CorsOptions>()
            .Configure<IOptions<AppCorsOptions>>((corsOptions, appCorsOptions) =>
            {
                corsOptions.AddPolicy(AppCorsOptions.PolicyName, policy =>
                {
                    if (environment.IsDevelopment())
                    {
                        policy.SetIsOriginAllowed(_ => true);
                    }
                    else
                    {
                        policy.WithOrigins(appCorsOptions.Value.AllowedOrigins);
                    }

                    policy.WithMethods(
                              HttpMethods.Get,
                              HttpMethods.Post,
                              HttpMethods.Put,
                              HttpMethods.Patch,
                              HttpMethods.Delete,
                              HttpMethods.Options)
                          .WithHeaders(
                              HeaderNames.Authorization,
                              HeaderNames.ContentType,
                              CustomHttpHeaders.XRequestedWith)
                          .AllowCredentials();
                });
            });

        return services;
    }
}
