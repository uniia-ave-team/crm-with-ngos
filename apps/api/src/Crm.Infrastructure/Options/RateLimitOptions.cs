using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

/// <summary>
/// Represents configuration options for rate limiting, defining time windows,
/// permit limits, and queue thresholds to protect the application from excessive traffic.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>
    /// Gets the configuration section key name used to bind rate limiting settings from appsettings.
    /// </summary>
    public const string Position = "RateLimiting";

    /// <summary>
    /// Gets or sets the duration of the time window in seconds during which requests are tracked and limited.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Window in seconds must be at least 1.")]
    public int WindowInSeconds { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of allowed requests permitted within the defined time window.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "Permit limit must be at least 1.")]
    public int PermitLimit { get; set; } = 50;

    /// <summary>
    /// Gets or sets the maximum number of queued requests allowed when the permit limit is reached.
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "Queue limit cannot be negative.")]
    public int QueueLimit { get; set; } = 0;
}
