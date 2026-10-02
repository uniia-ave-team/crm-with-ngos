namespace Crm.Domain.Consts;

/// <summary>
/// Provides constant values related to pagination across the application.
/// </summary>
public static class PaginationConstants
{
    /// <summary>
    /// Gets the minimum allowed page number (1-based index).
    /// </summary>
    public const int MinPageNumber = 1;

    /// <summary>
    /// Gets the minimum allowed page size.
    /// </summary>
    public const int MinPageSize = 1;

    /// <summary>
    /// Gets the maximum allowed page size to prevent database and network overload.
    /// </summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Gets the default page size used when no size is explicitly requested.
    /// </summary>
    public const int DefaultPageSize = 10;
}
