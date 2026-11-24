namespace Crm.Domain.Extensions;

/// <summary>
/// Provides domain-level string normalization, comparison, and file-name extraction helpers.
/// </summary>
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
    /// Normalizes a title by trimming all leading/trailing Unicode whitespace and converting to uppercase invariant
    /// for collation-independent uniqueness comparisons.
    /// </summary>
    /// <param name="title">The raw title to normalize.</param>
    /// <returns>The trimmed, uppercase-invariant normalized title.</returns>
    public static string NormalizeTitle(this string title)
    {
        ArgumentNullException.ThrowIfNull(title);
        return title.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// Extracts the base file name and extension from a path using either Unix (<c>/</c>) or Windows (<c>\</c>) separators,
    /// ensuring consistent behavior regardless of the host operating system.
    /// </summary>
    /// <param name="fileName">The raw client-supplied file name or path.</param>
    /// <returns>The base file name without directory components.</returns>
    public static string ExtractFileName(this string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        int lastSeparatorIndex = fileName.LastIndexOfAny(['/', '\\']);
        return lastSeparatorIndex >= 0 ? fileName[(lastSeparatorIndex + 1)..] : fileName;
    }
}
