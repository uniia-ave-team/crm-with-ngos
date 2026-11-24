using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

/// <summary>
/// Configuration options controlling the centralized entity audit log pipeline and retention policy.
/// </summary>
public sealed class AuditOptions
{
    /// <summary>
    /// The configuration section name in <c>appsettings.json</c>.
    /// </summary>
    public const string Position = "Auditing";

    /// <summary>
    /// Gets or sets a value indicating whether entity change auditing is active. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of audit records buffered in memory before oldest entries are dropped.
    /// </summary>
    [Range(100, 1_000_000)]
    public int QueueCapacity { get; set; } = 10_000;

    /// <summary>
    /// Gets or sets the maximum number of audit records written in a single sink batch.
    /// </summary>
    [Range(1, 5_000)]
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum delay in milliseconds before a partial batch is flushed to the sink.
    /// </summary>
    [Range(50, 60_000)]
    public int FlushIntervalMilliseconds { get; set; } = 500;

    /// <summary>
    /// Gets or sets the number of days to retain audit log entries before background pruning (0 disables pruning).
    /// </summary>
    [Range(0, 3650)]
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Gets or sets the interval in hours between retention cleanup runs.
    /// </summary>
    [Range(1, 168)]
    public int CleanupIntervalHours { get; set; } = 24;
}
