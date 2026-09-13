namespace Crm.Domain.Common;

/// <summary>
/// A generic wrapper class for paginated responses.
/// </summary>
/// <typeparam name="T">The type of the items in the paginated list.</typeparam>
public class PagedResult<T>(ICollection<T> items, int totalCount, int pageNumber, int pageSize)
{
    public ICollection<T> Items { get; } = items;

    public int TotalCount { get; } = totalCount;

    public int PageNumber { get; } = pageNumber;

    public int PageSize { get; } = pageSize;

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;
}
