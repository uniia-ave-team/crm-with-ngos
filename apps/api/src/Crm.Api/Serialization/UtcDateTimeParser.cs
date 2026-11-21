using System.Globalization;

namespace Crm.Api.Serialization;

/// <summary>
/// Provides shared UTC normalization and parsing helpers for <see cref="DateTime"/> values
/// received via JSON payloads or query/form model binding.
/// </summary>
public static class UtcDateTimeParser
{
    /// <summary>
    /// Normalizes a <see cref="DateTime"/> instance to <see cref="DateTimeKind.Utc"/>.
    /// Treats <see cref="DateTimeKind.Unspecified"/> as UTC and converts <see cref="DateTimeKind.Local"/> via <see cref="DateTime.ToUniversalTime"/>.
    /// </summary>
    /// <param name="value">The date/time value to normalize.</param>
    /// <returns>The UTC-normalized <see cref="DateTime"/> value.</returns>
    public static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified or _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    /// <summary>
    /// Attempts to parse an ISO 8601 or invariant date/time string into a <see cref="DateTimeKind.Utc"/> <see cref="DateTime"/>.
    /// Inputs without an explicit offset are treated as UTC; inputs with an offset are converted to UTC without local DST ambiguity.
    /// </summary>
    /// <param name="raw">The raw string representation to parse.</param>
    /// <param name="result">When this method returns <c>true</c>, contains the parsed UTC <see cref="DateTime"/>.</param>
    /// <returns><c>true</c> if parsing succeeded; otherwise, <c>false</c>.</returns>
    public static bool TryParseUtc(string? raw, out DateTime result)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
                out DateTimeOffset dto))
        {
            result = default;
            return false;
        }

        result = dto.UtcDateTime;
        return true;
    }
}
