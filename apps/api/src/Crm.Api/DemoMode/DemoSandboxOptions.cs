using System.ComponentModel.DataAnnotations;

namespace Crm.Api.DemoMode;

/// <summary>
/// Limits for the per-visitor demo sandboxes (private database copies).
/// </summary>
public sealed class DemoSandboxOptions
{
    /// <summary>
    /// Gets or sets the maximum number of sandboxes that may exist at the same time.
    /// </summary>
    [Range(1, 500)]
    public int MaxSandboxes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the number of minutes without requests after which a sandbox is deleted.
    /// </summary>
    [Range(1, 1440)]
    public int IdleTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum age of a sandbox in minutes, regardless of activity.
    /// </summary>
    [Range(1, 10080)]
    public int MaxLifetimeMinutes { get; set; } = 240;

    /// <summary>
    /// Gets or sets the idle time in seconds after which a sandbox may be evicted to make room
    /// for a new visitor when <see cref="MaxSandboxes"/> is reached.
    /// </summary>
    [Range(0, 86400)]
    public int EvictableAfterIdleSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets how many new sandboxes a single client address may create per
    /// <see cref="NewSandboxWindowMinutes"/>.
    /// </summary>
    [Range(1, 1000)]
    public int NewSandboxesPerAddress { get; set; } = 10;

    /// <summary>
    /// Gets or sets the length in minutes of the window used by <see cref="NewSandboxesPerAddress"/>.
    /// </summary>
    [Range(1, 1440)]
    public int NewSandboxWindowMinutes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of files a visitor may upload into one sandbox.
    /// </summary>
    [Range(0, 10000)]
    public int MaxUploadFiles { get; set; } = 20;

    /// <summary>
    /// Gets or sets the maximum total size in bytes of the files uploaded into one sandbox.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long MaxUploadBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// Gets or sets how often expired sandboxes are removed, in seconds.
    /// </summary>
    [Range(5, 3600)]
    public int CleanupIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Gets or sets the connection pool size used for each sandbox database.
    /// </summary>
    [Range(1, 50)]
    public int MaxPoolSize { get; set; } = 4;

    /// <summary>
    /// Gets the idle timeout as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan IdleTimeout => TimeSpan.FromMinutes(IdleTimeoutMinutes);

    /// <summary>
    /// Gets the maximum lifetime as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan MaxLifetime => TimeSpan.FromMinutes(MaxLifetimeMinutes);

    /// <summary>
    /// Gets the minimum idle time before eviction as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan EvictableAfterIdle => TimeSpan.FromSeconds(EvictableAfterIdleSeconds);
}
