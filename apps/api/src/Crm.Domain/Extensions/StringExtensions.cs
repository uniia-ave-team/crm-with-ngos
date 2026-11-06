namespace Crm.Domain.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Determines whether the string equals any of the specified target strings, ignoring case.
    /// </summary>
    /// <param name="source">The input string to compare.</param>
    /// <param name="targets">The target strings to compare against.</param>
    /// <returns><c>true</c> if the source matches any target; otherwise, <c>false</c>.</returns>
    public static bool EqualsAnyIgnoreCase(this string? source, params string[] targets)
    {
        return source is not null && targets.Length != 0 && targets.Any(t => string.Equals(source, t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Formats a search term into a wildcard pattern.
    /// </summary>
    /// <param name="term">The input search phrase.</param>
    /// <param name="escapeChar">The escape character used in SQL ILike clause.</param>
    /// <returns>A formatted pattern string with leading and trailing percent signs.</returns>
    public static string ToContainsLikePattern(this string term, string escapeChar = "\\")
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return string.Empty;
        }

        var escaped = term.Trim()
            .Replace(escapeChar, $"{escapeChar}{escapeChar}")
            .Replace("%", $"{escapeChar}%")
            .Replace("_", $"{escapeChar}_");

        return $"%{escaped}%";
    }
}
