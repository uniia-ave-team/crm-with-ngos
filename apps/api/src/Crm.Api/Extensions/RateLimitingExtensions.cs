using Crm.Api.Options;
using Crm.Api.RateLimiting;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for configuring HTTP rate limiting policies in the API layer.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Registers and validates <see cref="RateLimitOptions"/> and configures the global IP-based rate limiter policy.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The original <see cref="IServiceCollection"/> instance for chaining.</returns>
    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.Position))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, IpRateLimiterPolicy>(RateLimitOptions.PolicyName);
        });

        return services;
    }
}
