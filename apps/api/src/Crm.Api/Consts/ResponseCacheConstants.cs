namespace Crm.Api.Consts;

/// <summary>
/// Provides constant values for HTTP response caching durations and policies.
/// </summary>
public static class ResponseCacheConstants
{
    /// <summary>
    /// Response cache duration of 1 hour (3600 seconds).
    /// </summary>
    public const int OneHour = 60 * 60;

    /// <summary>
    /// Response cache duration of 1 day / 24 hours (86400 seconds).
    /// </summary>
    public const int OneDay = 60 * 60 * 24;

    /// <summary>
    /// Response cache duration of 7 days (604800 seconds), ideal for static public files like login page images.
    /// </summary>
    public const int SevenDays = 60 * 60 * 24 * 7;

    /// <summary>
    /// Response cache duration of 30 days (2592000 seconds), ideal for long-lived assets like logos or avatars.
    /// </summary>
    public const int ThirtyDays = 60 * 60 * 24 * 30;
}
