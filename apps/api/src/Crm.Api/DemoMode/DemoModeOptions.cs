using System.ComponentModel.DataAnnotations;

namespace Crm.Api.DemoMode;

/// <summary>
/// Represents configuration options for Demo Mode instances.
/// When enabled, mutating operations run in dry-run mode (rolled back before committing),
/// authentication refresh tokens are kept in a bounded in-memory store, and the database remains read-only.
/// </summary>
public sealed class DemoModeOptions
{
    /// <summary>
    /// Gets the configuration section name used to bind demo mode settings from appsettings.
    /// </summary>
    public const string Position = "DemoMode";

    /// <summary>
    /// Gets the minimum number of path segments required for configured path prefixes
    /// (for example <c>/api/v1/users</c>) so overly broad prefixes like <c>/</c> or <c>/api/v1</c>
    /// cannot silently disable Demo Mode protection.
    /// </summary>
    public const int MinimumPathSegmentCount = 3;

    /// <summary>
    /// Gets the default request path prefixes excluded from Demo Mode restrictions.
    /// </summary>
    public static readonly string[] DefaultExcludedPaths =
    [
        "/api/v1/auth/login",
        "/api/v1/auth/refresh-token",
        "/api/v1/auth/logout",
    ];

    /// <summary>
    /// Gets or sets a value indicating whether demo mode is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets request path prefixes excluded from demo mode restrictions.
    /// Defaults to authentication endpoints (<c>login</c>, <c>refresh-token</c>, <c>logout</c>).
    /// When configured explicitly in appsettings or environment variables, replaces the defaults.
    /// </summary>
    [Required]
    public string[] ExcludedPaths { get; set; } = [.. DefaultExcludedPaths];

    /// <summary>
    /// Gets or sets optional request path prefixes whose write operations are strictly blocked with
    /// <c>403 Forbidden</c> (<c>DEMO_MODE_RESTRICTED</c>) when demo mode is enabled.
    /// Defaults to an empty array so all mutating endpoints execute in transactional dry-run mode.
    /// </summary>
    [Required]
    public string[] DemoBlockedPaths { get; set; } = [];

    /// <summary>
    /// Gets or sets the maximum number of active refresh tokens retained in the bounded in-memory
    /// Demo Mode token store to prevent memory exhaustion under repeated public demo logins.
    /// </summary>
    [Range(100, 100_000)]
    public int MaxInMemoryRefreshTokens { get; set; } = 10_000;

    /// <summary>
    /// Normalizes a path prefix by trimming whitespace, ensuring a single leading <c>'/'</c>,
    /// and removing trailing slashes (except for root <c>"/"</c>).
    /// </summary>
    /// <param name="path">The raw path prefix string.</param>
    /// <returns>The normalized path prefix, or <see langword="null"/> if <paramref name="path"/> is null or whitespace.</returns>
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
    /// Determines whether a normalized path prefix is valid and specific enough for segment matching
    /// (must start with <c>'/'</c>, contain no query strings or fragments, and have at least
    /// <see cref="MinimumPathSegmentCount"/> non-empty path segments).
    /// </summary>
    /// <param name="path">The path prefix to validate.</param>
    /// <returns><see langword="true"/> if valid; otherwise, <see langword="false"/>.</returns>
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
    /// Normalizes all configured path prefixes in <see cref="ExcludedPaths"/> and <see cref="DemoBlockedPaths"/>.
    /// </summary>
    public void NormalizePaths()
    {
        ExcludedPaths = NormalizeArray(ExcludedPaths);
        DemoBlockedPaths = NormalizeArray(DemoBlockedPaths);
    }

    private static string[] NormalizeArray(string[]? paths) =>
        paths is null
            ? []
            : [.. paths
                .Select(NormalizePathPrefix)
                .Where(static p => p is not null)
                .Select(static p => p!)
                .Distinct(StringComparer.OrdinalIgnoreCase)];
}
