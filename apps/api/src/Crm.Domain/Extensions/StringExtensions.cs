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

    /// <summary>
    /// Builds a sanitized PostgreSQL <c>to_tsquery</c> prefix query expression (e.g., <c>"kyi:*"</c> or <c>"kyi:* &amp; don:*"</c>)
    /// from raw user search input. Non-alphanumeric characters are treated as token delimiters to avoid tsquery syntax errors.
    /// </summary>
    /// <param name="searchTerm">The raw search string provided by the caller.</param>
    /// <returns>A formatted tsquery string with prefix wildcards, or <see langword="null"/> if no searchable tokens exist.</returns>
    public static string? BuildPrefixTsQuery(this string? searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return null;
        }

        var span = searchTerm.AsSpan().Trim();
        var tokens = new List<string>();
        int start = -1;

        for (int i = 0; i < span.Length; i++)
        {
            char c = span[i];
            bool isTokenChar = char.IsLetterOrDigit(c);

            if (isTokenChar)
            {
                if (start < 0)
                {
                    start = i;
                }
            }
            else if (start >= 0)
            {
                tokens.Add($"{span[start..i].ToString().ToLowerInvariant()}:*");
                start = -1;
            }
        }

        if (start >= 0)
        {
            tokens.Add($"{span[start..].ToString().ToLowerInvariant()}:*");
        }

        return tokens.Count == 0 ? null : string.Join(" & ", tokens);
    }
}
