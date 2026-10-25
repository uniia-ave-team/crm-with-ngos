using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="CustomTable"/> entities and their field definitions.
/// </summary>
public interface ICustomTableRepository : IGenericRepository<CustomTable>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom tables.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="ngoId">Filter to retrieve custom tables belonging to a specific NGO.</param>
    /// <param name="searchTerm">Optional search term to filter custom tables by Name or Description.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected custom table items.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid ngoId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a custom table by its unique identifier along with its fields for updating, with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked <see cref="CustomTable"/> entity including its related fields.</returns>
    /// <exception cref="NotFoundException">Thrown if the custom table with the specified ID is not found.</exception>
    Task<CustomTable> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default);
}
