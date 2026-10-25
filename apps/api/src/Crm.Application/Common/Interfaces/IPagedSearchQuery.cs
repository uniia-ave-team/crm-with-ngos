namespace Crm.Application.Common.Interfaces;

/// <summary>
/// Defines a contract for queries that support pagination, sorting, and text searching.
/// </summary>
public interface IPagedSearchQuery
{
    /// <summary>
    /// Gets the page index number (1-based).
    /// </summary>
    int PageNumber { get; }

    /// <summary>
    /// Gets the maximum number of items per page.
    /// </summary>
    int PageSize { get; }

    /// <summary>
    /// Gets the target field or property name used for sorting.
    /// </summary>
    string? OrderBy { get; }

    /// <summary>
    /// Gets the sorting direction (e.g., ascending or descending).
    /// </summary>
    string? SortOrder { get; }

    /// <summary>
    /// Gets the search keyword or term used to filter records.
    /// </summary>
    string? SearchTerm { get; }
}
