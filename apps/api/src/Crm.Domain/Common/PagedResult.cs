namespace Crm.Domain.Common;

/// <summary>
/// A generic wrapper class for paginated query results.
/// </summary>
/// <typeparam name="T">The type of the items in the paginated page.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PagedResult{T}"/> class.
    /// </summary>
    /// <param name="items">The items belonging to the current page.</param>
    /// <param name="totalCount">The total number of matching items across all pages.</param>
    /// <param name="pageNumber">The 1-based current page number.</param>
    /// <param name="pageSize">The maximum number of items per page.</param>
    public PagedResult(IReadOnlyCollection<T> items, int totalCount, int pageNumber, int pageSize)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(pageSize);

        Items = items;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    /// <summary>
    /// Gets the items in the current page.
    /// </summary>
    public IReadOnlyCollection<T> Items { get; }

    /// <summary>
    /// Gets the total number of matching items across all pages.
    /// </summary>
    public int TotalCount { get; }

    /// <summary>
    /// Gets the 1-based index of the current page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the maximum number of items per page.
    /// </summary>
    public int PageSize { get; }

    /// <summary>
    /// Gets the total number of pages available for <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;

    /// <summary>
    /// Gets a value indicating whether a previous page exists before <see cref="PageNumber"/>.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets a value indicating whether a subsequent page exists after <see cref="PageNumber"/>.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;
}
