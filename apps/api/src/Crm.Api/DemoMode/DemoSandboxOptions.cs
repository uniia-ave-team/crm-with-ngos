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
    /// Gets or sets how long a sandbox lives, in minutes, counted from the moment it is created.
    /// The visitor sees this as a countdown; activity does not extend it.
    /// </summary>
    [Range(1, 10080)]
    public int LifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the idle time in seconds after which a sandbox may be evicted to make room
    /// for a new visitor when <see cref="MaxSandboxes"/> is reached.
    /// </summary>
    [Range(0, 604800)]
    public int EvictableAfterIdleSeconds { get; set; } = 600;

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
    /// Gets the sandbox lifetime as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes);

    /// <summary>
    /// Gets the minimum idle time before eviction as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan EvictableAfterIdle => TimeSpan.FromSeconds(EvictableAfterIdleSeconds);
}
