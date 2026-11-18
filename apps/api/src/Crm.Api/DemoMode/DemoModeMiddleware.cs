using Crm.Api.Consts;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Middleware that blocks explicitly configured sensitive write operations (<c>POST</c>, <c>PUT</c>, <c>PATCH</c>, <c>DELETE</c>)
/// marked with <see cref="DemoBlockedAttribute"/> or configured in <see cref="DemoModeOptions.DemoBlockedPaths"/>
/// when Demo Mode is active, while letting other write operations flow to the transactional dry-run pipeline.
/// </summary>
/// <param name="next">The next middleware delegate in the HTTP request pipeline.</param>
/// <param name="demoModeOptions">The options providing <see cref="DemoModeOptions"/> configuration.</param>
/// <param name="logger">The logger instance for recording blocked write requests.</param>
public sealed partial class DemoModeMiddleware(
    RequestDelegate next,
    IOptions<DemoModeOptions> demoModeOptions,
    ILogger<DemoModeMiddleware> logger)
{
    internal const string RestrictedErrorCode = "DEMO_MODE_RESTRICTED";
    private const string RestrictedErrorMessage = "This operation is disabled in Demo Mode.";

    /// <summary>
    /// Processes an incoming HTTP request, blocking configured sensitive write endpoints when Demo Mode is enabled.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
    /// <returns>A task that represents the completion of request processing.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var options = demoModeOptions.Value;

        if (options.Enabled &&
            IsWriteMethod(context.Request.Method) &&
            !IsMatchingPath(context.Request.Path, options.ExcludedPaths) &&
            IsBlockedEndpoint(context, options.DemoBlockedPaths))
        {
            LogDemoModeWriteBlocked(logger, context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    type = ProblemDetailsDefaults.GetTypeUrl(StatusCodes.Status403Forbidden),
                    title = ProblemDetailsDefaults.ClientErrorTitle,
                    status = StatusCodes.Status403Forbidden,
                    detail = RestrictedErrorMessage,
                    instance = context.Request.Path.Value,
                    errorCode = RestrictedErrorCode,
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: context.RequestAborted);

            return;
        }

        await next(context);
    }

    internal static bool IsMatchingPath(PathString path, string[]? configuredPaths)
    {
        if (configuredPaths is not { Length: > 0 })
        {
            return false;
        }

        foreach (var rawPrefix in configuredPaths)
        {
            var normalized = DemoModeOptions.NormalizePathPrefix(rawPrefix);
            if (normalized is not null && path.StartsWithSegments(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWriteMethod(string method) =>
        HttpMethods.IsPost(method) ||
        HttpMethods.IsPut(method) ||
        HttpMethods.IsPatch(method) ||
        HttpMethods.IsDelete(method);

    private static bool IsBlockedEndpoint(HttpContext context, string[]? blockedPaths) =>
        context.GetEndpoint()?.Metadata.GetMetadata<DemoBlockedAttribute>() is not null ||
        IsMatchingPath(context.Request.Path, blockedPaths);

    [LoggerMessage(EventId = DemoLogEventIds.DemoModeWriteBlocked, Level = LogLevel.Warning, Message = "Demo mode blocked sensitive write request: {Method} {Path}")]
    private static partial void LogDemoModeWriteBlocked(ILogger logger, string method, PathString path);
}
