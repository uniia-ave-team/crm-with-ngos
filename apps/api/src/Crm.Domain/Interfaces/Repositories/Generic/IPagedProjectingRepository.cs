using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;

namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines read-only operations for retrieving paginated, filtered, and projected entities.
/// </summary>
/// <typeparam name="T">The type of the underlying domain entity.</typeparam>
public interface IPagedProjectingRepository<T>
    where T : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="predicate">
    /// Expression that MUST be translatable to SQL by EF Core.
    /// Non-translatable expressions will cause client-side evaluation.
    /// </param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction ("asc" or "desc").</param>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Expression<Func<T, bool>>? predicate = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default);
}
