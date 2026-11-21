using System.ComponentModel.DataAnnotations;

namespace Crm.Api.DemoMode;

/// <summary>
/// Configuration of the public demo mode. When enabled, every visitor who signs in works in a private,
/// short-lived copy of the demo database (a sandbox), so visitors never see each other's changes.
/// </summary>
public sealed class DemoModeOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string Position = "DemoMode";

    /// <summary>
    /// The minimum number of path segments a blocked path prefix must have (for example <c>/api/v1/users</c>).
    /// </summary>
    public const int MinimumPathSegmentCount = 3;

    /// <summary>
    /// Gets or sets a value indicating whether demo mode is on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets extra path prefixes whose write requests are rejected in demo mode,
    /// in addition to the endpoints marked with <see cref="DemoBlockedAttribute"/>.
    /// </summary>
    [Required]
    public string[] DemoBlockedPaths { get; set; } = [];

    /// <summary>
    /// Gets or sets the sandbox limits.
    /// </summary>
    [Required]
    public DemoSandboxOptions Sandbox { get; set; } = new();

    /// <summary>
    /// Gets or sets the demo accounts shown on the sign-in page. Their credentials are public by design.
    /// When empty, <see cref="DemoAccountOptions.Defaults"/> are used.
    /// </summary>
    [Required]
    public DemoAccountOptions[] Accounts { get; set; } = [];

    /// <summary>
    /// Normalizes a path prefix: adds the leading slash and removes the trailing one.
    /// </summary>
    /// <param name="path">The configured path prefix.</param>
    /// <returns>The normalized prefix, or <c>null</c> when the input is empty.</returns>
    public static string? NormalizePathPrefix(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string trimmed = "/" + path.Trim().TrimStart('/');

        if (trimmed.Length > 1)
        {
            trimmed = trimmed.TrimEnd('/');
        }

        return trimmed;
    }

    /// <summary>
    /// Checks that a path prefix is specific enough to be used as a blocked path.
    /// </summary>
    /// <param name="path">The normalized path prefix.</param>
    /// <returns><c>true</c> when the prefix is valid.</returns>
    public static bool IsValidPathPrefix(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.StartsWith('/') ||
            path.Contains('?') ||
            path.Contains('#'))
        {
            return false;
        }

        int segmentCount = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length;

        return segmentCount >= MinimumPathSegmentCount;
    }

    /// <summary>
    /// Gets the configured demo accounts, or the defaults when none are configured.
    /// </summary>
    /// <returns>The effective list of demo accounts.</returns>
    public IReadOnlyList<DemoAccountOptions> GetEffectiveAccounts() =>
        Accounts is { Length: > 0 } ? Accounts : DemoAccountOptions.Defaults;

    /// <summary>
    /// Normalizes all configured path prefixes in place.
    /// </summary>
    public void NormalizePaths()
    {
        DemoBlockedPaths = DemoBlockedPaths is null
            ? []
            : [.. DemoBlockedPaths
                .Select(NormalizePathPrefix)
                .Where(static p => p is not null)
                .Select(static p => p!)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
