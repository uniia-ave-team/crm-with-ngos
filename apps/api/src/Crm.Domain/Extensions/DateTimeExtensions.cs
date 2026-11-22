namespace Crm.Domain.Extensions;

/// <summary>
/// Provides UTC normalization helpers guaranteeing that all domain and persistence timestamps
/// use UTC+0 without timezone offset drift.
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// Normalizes a <see cref="DateTime"/> value to <see cref="DateTimeKind.Utc"/>.
    /// Local values are converted to UTC; unspecified values are tagged as UTC without shifting clock ticks.
    /// </summary>
    /// <param name="value">The timestamp to normalize.</param>
    /// <returns>A <see cref="DateTime"/> instance with <see cref="DateTimeKind.Utc"/>.</returns>
    public static DateTime ToUtc(this DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified or _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    /// <summary>
    /// Normalizes a nullable <see cref="DateTime"/> value to <see cref="DateTimeKind.Utc"/> when present.
    /// </summary>
    /// <param name="value">The nullable timestamp to normalize.</param>
    /// <returns>A UTC <see cref="DateTime"/> value, or <c>null</c> when <paramref name="value"/> is <c>null</c>.</returns>
    public static DateTime? ToUtc(this DateTime? value)
        => value?.ToUtc();

    /// <summary>
    /// Normalizes a <see cref="DateTimeOffset"/> value to UTC+0 (<see cref="TimeSpan.Zero"/> offset).
    /// </summary>
    /// <param name="value">The timestamp with offset to normalize.</param>
    /// <returns>An equivalent <see cref="DateTimeOffset"/> with zero UTC offset.</returns>
    public static DateTimeOffset ToUtc(this DateTimeOffset value)
        => value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();

    /// <summary>
    /// Normalizes a nullable <see cref="DateTimeOffset"/> value to UTC+0 (<see cref="TimeSpan.Zero"/> offset) when present.
    /// </summary>
    /// <param name="value">The nullable timestamp with offset to normalize.</param>
    /// <returns>An equivalent UTC+0 <see cref="DateTimeOffset"/>, or <c>null</c> when <paramref name="value"/> is <c>null</c>.</returns>
    public static DateTimeOffset? ToUtc(this DateTimeOffset? value)
        => value?.ToUtc();
}
