using Crm.Api.Extensions;
using Serilog.Events;

namespace Crm.Api.Logging;

/// <summary>
/// Resolves Serilog request logging levels based on HTTP status code and exception presence.
/// </summary>
public static class SerilogLogLevelResolver
{
    public static LogEventLevel Resolve(HttpContext httpContext, Exception? exception)
        => exception is not null
        ? ResolveByException(exception)
        : ResolveByStatusCode(httpContext.Response.StatusCode);

    private static LogEventLevel ResolveByException(Exception exception) =>
        exception.MapToStatusCode() >= StatusCodes.Status500InternalServerError
            ? LogEventLevel.Error
            : LogEventLevel.Warning;

    private static LogEventLevel ResolveByStatusCode(int statusCode) => statusCode switch
    {
        >= StatusCodes.Status400BadRequest and < StatusCodes.Status500InternalServerError => LogEventLevel.Warning,
        >= StatusCodes.Status500InternalServerError => LogEventLevel.Error,
        _ => LogEventLevel.Information,
    };
}
