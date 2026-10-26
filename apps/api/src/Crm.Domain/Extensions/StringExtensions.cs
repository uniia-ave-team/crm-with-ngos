namespace Crm.Domain.Extensions;

public static class StringExtensions
{
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
