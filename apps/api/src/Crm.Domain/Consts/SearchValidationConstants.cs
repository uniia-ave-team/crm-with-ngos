namespace Crm.Domain.Consts;

/// <summary>
/// Provides constant values related to search query validation across the application.
/// </summary>
public static class SearchValidationConstants
{
    /// <summary>
    /// Gets the minimum allowed length for a search term to prevent sequence scans and ensure query performance.
    /// </summary>
    public const int MinSearchTermLength = 3;

    /// <summary>
    /// Gets the maximum allowed length for a search term to prevent query overload and DoS attacks.
    /// </summary>
    public const int MaxSearchTermLength = 100;
}
