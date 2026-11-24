namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides PostgreSQL-specific string formatting helpers for <c>ILIKE</c> and <c>to_tsquery</c> queries.
/// </summary>
public static class PostgresSearchStringExtensions
{
    /// <summary>
    /// Formats a search term into an escaped SQL <c>ILIKE</c> contains wildcard pattern.
    /// </summary>
    /// <param name="term">The input search phrase.</param>
    /// <param name="escapeChar">The escape character used in the SQL <c>ILIKE</c> clause.</param>
    /// <returns>A formatted pattern string with leading and trailing percent signs, or empty string if blank.</returns>
    public static string ToContainsLikePattern(this string? term, string escapeChar = "\\")
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
