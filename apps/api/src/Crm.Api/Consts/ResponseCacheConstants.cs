namespace Crm.Api.Consts;

/// <summary>
/// Provides constant values for HTTP response caching durations.
/// </summary>
public static class ResponseCacheConstants
{
    /// <summary>
    /// Response cache duration of 30 days (2592000 seconds), ideal for long-lived versioned assets like logos, avatars, or login page images.
    /// </summary>
    public const int ThirtyDays = 60 * 60 * 24 * 30;
}
