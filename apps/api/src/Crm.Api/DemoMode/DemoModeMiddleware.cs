using System.Globalization;
using System.Text.Json;
using Crm.Api.Consts;
using Crm.Api.DemoMode.Sandbox;
using Crm.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Decides which copy of the demo data a request works with. It must run after authentication
/// and before authorization, because authorization already reads role permissions from the database.
/// <list type="bullet">
///   <item><description>Write requests to endpoints marked <see cref="DemoBlockedAttribute"/> are rejected.</description></item>
///   <item><description>Signing in starts a sandbox (or continues the visitor's previous one).</description></item>
///   <item><description>Authenticated requests and token refreshes run in the sandbox named in the access token.</description></item>
///   <item><description>Other anonymous requests read the shared demo database, which is write-protected.</description></item>
/// </list>
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="demoModeOptions">The demo mode options.</param>
/// <param name="sandboxManager">The sandbox manager.</param>
/// <param name="logger">The logger.</param>
public sealed partial class DemoModeMiddleware(
    RequestDelegate next,
    IOptions<DemoModeOptions> demoModeOptions,
    DemoSandboxManager sandboxManager,
    ILogger<DemoModeMiddleware> logger)
{
    /// <summary>
    /// The cookie that lets a visitor who signs in again (for example under another demo account)
    /// continue in the same sandbox. Identity always comes from the access token, never from this cookie.
    /// </summary>
    public const string SandboxCookieName = "demo_sid";

    private const string AccessTokenProperty = "accessToken";
    private const int MaxRefreshBodyBytes = 64 * 1024;
    private const int CapacityRetryAfterSeconds = 60;
    private const string UnknownAddress = "unknown";

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that completes when the request is processed.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var options = demoModeOptions.Value;
        var endpoint = context.GetEndpoint();

        if (IsWriteMethod(context.Request.Method) && IsBlockedEndpoint(context, endpoint, options.DemoBlockedPaths))
        {
            LogDemoModeWriteBlocked(logger, context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, DemoErrorCodes.Restricted, DemoErrorCodes.RestrictedMessage);
            return;
        }

        var sessionEndpoint = endpoint?.Metadata.GetMetadata<DemoSessionEndpointAttribute>();

        if (sessionEndpoint?.Kind == DemoSessionEndpointKind.SignIn)
        {
            await HandleSignInAsync(context, options.Sandbox);
            return;
        }

        string? sandboxId = sessionEndpoint?.Kind == DemoSessionEndpointKind.Refresh
            ? await ReadSandboxIdFromRefreshBodyAsync(context)
            : context.User.FindFirst(DemoClaimTypes.SandboxId)?.Value;

        bool needsSandbox = sessionEndpoint is not null || context.User.Identity?.IsAuthenticated == true;

        if (!needsSandbox)
        {
            await next(context);
            return;
        }

        if (!sandboxManager.TryGet(sandboxId, out var sandbox))
        {
            LogSessionExpired(logger, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, DemoErrorCodes.SessionExpired, DemoErrorCodes.SessionExpiredMessage);
            return;
        }

        DemoSandboxAccessor.SetSandbox(context, sandbox);

        await next(context);
    }

    /// <summary>
    /// Checks whether a request path starts with one of the configured prefixes.
    /// </summary>
    /// <param name="path">The request path.</param>
    /// <param name="configuredPaths">The configured path prefixes.</param>
    /// <returns><c>true</c> when the path matches.</returns>
    internal static bool IsMatchingPath(PathString path, string[]? configuredPaths)
    {
        if (configuredPaths is not { Length: > 0 })
        {
            return false;
        }

        foreach (string rawPrefix in configuredPaths)
        {
            string? normalized = DemoModeOptions.NormalizePathPrefix(rawPrefix);

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

    private static bool IsBlockedEndpoint(HttpContext context, Endpoint? endpoint, string[]? blockedPaths) =>
        endpoint?.Metadata.GetMetadata<DemoBlockedAttribute>() is not null ||
        IsMatchingPath(context.Request.Path, blockedPaths);

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string errorCode, string detail)
    {
        context.Response.StatusCode = statusCode;

        await context.Response.WriteAsJsonAsync(
            new
            {
                type = ProblemDetailsDefaults.GetTypeUrl(statusCode),
                title = statusCode < StatusCodes.Status500InternalServerError
                    ? ProblemDetailsDefaults.ClientErrorTitle
                    : ProblemDetailsDefaults.ServerErrorTitle,
                status = statusCode,
                detail,
                instance = context.Request.Path.Value,
                errorCode,
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static void WriteSandboxCookie(HttpContext context, DemoSandbox sandbox, DemoSandboxOptions options) =>
        context.Response.Cookies.Append(
            SandboxCookieName,
            sandbox.Id,
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = context.Request.IsHttps,
                Path = "/",
                MaxAge = options.MaxLifetime,
            });

    private async Task HandleSignInAsync(HttpContext context, DemoSandboxOptions options)
    {
        // A visitor who signs in again (for example under another demo account) keeps the same sandbox.
        bool created = false;

        if (!sandboxManager.TryGet(context.Request.Cookies[SandboxCookieName], out var sandbox))
        {
            string clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? UnknownAddress;

            if (!sandboxManager.TryAcquireCreationPermit(clientAddress))
            {
                context.Response.Headers.RetryAfter =
                    (options.NewSandboxWindowMinutes * 60).ToString(CultureInfo.InvariantCulture);
                await WriteProblemAsync(context, StatusCodes.Status429TooManyRequests, DemoErrorCodes.TooManySessions, DemoErrorCodes.TooManySessionsMessage);
                return;
            }

            sandbox = await sandboxManager.CreateAsync(context.RequestAborted);

            if (sandbox is null)
            {
                context.Response.Headers.RetryAfter = CapacityRetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                await WriteProblemAsync(context, StatusCodes.Status503ServiceUnavailable, DemoErrorCodes.CapacityReached, DemoErrorCodes.CapacityReachedMessage);
                return;
            }

            created = true;
        }

        DemoSandboxAccessor.SetSandbox(context, sandbox);

        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode == StatusCodes.Status200OK)
            {
                WriteSandboxCookie(context, sandbox, options);
            }
            else if (!created)
            {
                // The visitor may have changed the account inside the old sandbox (password, deactivation).
                // Forgetting it makes the next attempt start from fresh demo data.
                context.Response.Cookies.Delete(SandboxCookieName, new CookieOptions { Path = "/" });
            }

            return Task.CompletedTask;
        });

        bool succeeded = false;

        try
        {
            await next(context);
            succeeded = context.Response.StatusCode == StatusCodes.Status200OK;
        }
        finally
        {
            // A failed sign-in (it reaches the exception handler as an exception) must not leave a sandbox behind.
            if (created && !succeeded)
            {
                await sandboxManager.RemoveAsync(sandbox.Id, CancellationToken.None);
            }
        }
    }

    private static async Task<string?> ReadSandboxIdFromRefreshBodyAsync(HttpContext context)
    {
        // The refresh request is anonymous: the sandbox is named by the (possibly expired) access token in its body.
        if (context.Request.ContentLength > MaxRefreshBodyBytes)
        {
            return null;
        }

        context.Request.EnableBuffering(MaxRefreshBodyBytes, MaxRefreshBodyBytes);

        string? accessToken = null;

        try
        {
            using var document = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);

            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (property.Name.Equals(AccessTokenProperty, StringComparison.OrdinalIgnoreCase) &&
                        property.Value.ValueKind == JsonValueKind.String)
                    {
                        accessToken = property.Value.GetString();
                        break;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            // Not JSON, or larger than the buffer limit.
            return null;
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        var tokenService = context.RequestServices.GetRequiredService<ITokenService>();

        return tokenService.GetPrincipalFromExpiredToken(accessToken)?.FindFirst(DemoClaimTypes.SandboxId)?.Value;
    }

    [LoggerMessage(EventId = DemoLogEventIds.DemoModeWriteBlocked, Level = LogLevel.Warning, Message = "Demo mode blocked a restricted write request: {Method} {Path}")]
    private static partial void LogDemoModeWriteBlocked(ILogger logger, string method, PathString path);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxSessionExpired, Level = LogLevel.Information, Message = "Demo mode rejected {Path}: the sandbox of the request no longer exists.")]
    private static partial void LogSessionExpired(ILogger logger, PathString path);
}
