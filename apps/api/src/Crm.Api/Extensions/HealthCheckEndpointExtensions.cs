using Crm.Api.HealthChecks;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Crm.Api.Extensions;

/// <summary>
/// Extension methods for mapping health check endpoints.
/// </summary>
public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Maps a custom health check endpoint with a formatted JSON response.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder used to define routes.</param>
    /// <param name="path">The URL path for the health check endpoint. Defaults to "/health".</param>
    /// <returns>The same endpoint route builder instance for chaining.</returns>
    public static IEndpointRouteBuilder MapCustomHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string path = "/health")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks(path, new HealthCheckOptions
        {
            ResponseWriter = HealthCheckResponseWriter.WriteJsonResponse,
        });

        return endpoints;
    }
}
