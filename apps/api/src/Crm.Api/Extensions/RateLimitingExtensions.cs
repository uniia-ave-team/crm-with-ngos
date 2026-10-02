using Crm.Infrastructure.Options;
using Crm.Infrastructure.RateLimiting;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for registering and configuring rate limiting services in the dependency injection container.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Gets the name of the global rate limiting policy applied across the application.
    /// </summary>
    public const string GlobalPolicyName = "Global";

    /// <summary>
    /// Configures and registers global rate limiting services using a fixed window limiter policy
    /// bound from the application configuration with pre-startup data annotation validation.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <param name="configuration">The application <see cref="IConfiguration"/> used to bind rate limiting options.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
                .Bind(configuration.GetSection(RateLimitOptions.Position))
                .ValidateDataAnnotations()
                .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, IpRateLimiterPolicy>(GlobalPolicyName);
        });

        return services;
    }
}
