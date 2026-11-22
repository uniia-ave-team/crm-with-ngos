using Crm.Api.Consts;

namespace Crm.Api.Extensions;

/// <summary>
/// Provides extension methods for applying HTTP security response headers across the pipeline.
/// </summary>
public static class SecurityHeadersExtensions
{
    /// <summary>
    /// Adds defensive security response headers (such as <c>X-Content-Type-Options: nosniff</c>)
    /// to all HTTP responses produced by the application.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
    /// <returns>The original <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use((context, next) =>
        {
            context.Response.Headers.XContentTypeOptions = SecurityHeaderValues.NoSniff;
            return next(context);
        });
    }
}
