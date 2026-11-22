using System.Net;
using System.Threading.RateLimiting;
using Crm.Api.Options;
using Crm.Application.Common.Consts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Crm.Api.RateLimiting;

/// <summary>
/// Represents a custom rate-limiting policy based on the client's IP address.
/// Implements <see cref="IRateLimiterPolicy{TKey}"/> using a fixed window algorithm
/// configured via options and provides structured logging for rejected requests.
/// </summary>
/// <param name="options">The options containing rate limit parameters such as permit limit, window size, and queue limit.</param>
/// <param name="logger">The logger used to record rate-limiting violations and security warnings.</param>
public sealed partial class IpRateLimiterPolicy(
    IOptions<RateLimitOptions> options,
    ILogger<IpRateLimiterPolicy> logger) : IRateLimiterPolicy<string>
{
    private const string UnknownIpAddress = "unknown";
    private readonly RateLimitOptions _options = options.Value;

    /// <summary>
    /// Gets the callback that is invoked when a request is rejected due to rate limiting.
    /// Logs a warning with the client's IP address and sets the response status code to 429 (Too Many Requests).
    /// </summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) =>
        {
            LogRateLimitExceeded(logger, context.HttpContext.Connection.RemoteIpAddress);
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return default;
        };

    /// <summary>
    /// Determines the rate limit partition for the incoming HTTP request based on the client's IP address.
    /// Uses a fixed-window limiter strategy isolated per individual IP.
    /// </summary>
    /// <param name="httpContext">The active <see cref="HttpContext"/> of the current request.</param>
    /// <returns>A <see cref="RateLimitPartition{TKey}"/> configured with fixed-window options for the target client IP.</returns>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        string clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownIpAddress;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientIp,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = _options.PermitLimit,
                Window = TimeSpan.FromSeconds(_options.WindowInSeconds),
                QueueLimit = _options.QueueLimit,
            });
    }

    [LoggerMessage(EventId = LogEventIds.RateLimitExceeded, Level = LogLevel.Warning, Message = "Rate limit exceeded for IP {IpAddress}.")]
    private static partial void LogRateLimitExceeded(ILogger logger, IPAddress? ipAddress);
}
