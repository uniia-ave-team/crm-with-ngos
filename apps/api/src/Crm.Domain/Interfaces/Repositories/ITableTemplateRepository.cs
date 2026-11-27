using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing <see cref="TableTemplate"/> entities.
/// </summary>
public interface ITableTemplateRepository : IGenericRepository<TableTemplate>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected table templates.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="onlySystemPresets">Optional filter to retrieve only system presets (<c>true</c>), only user templates (<c>false</c>), or all (<c>null</c>).</param>
    /// <param name="searchTerm">Optional search keyword applied to template names and descriptions.</param>
    /// <param name="orderBy">The property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the matching projected table templates.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        bool? onlySystemPresets = null,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a pessimistic row-level write lock (<c>FOR NO KEY UPDATE</c>) on the table template row
    /// within the active transaction to serialize concurrent modifications.
    /// </summary>
    /// <param name="id">The unique identifier of the table template to lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous lock operation.</returns>
    Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all existing tracked system preset templates for synchronization.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A list containing all tracked system preset <see cref="TableTemplate"/> entities.</returns>
    Task<List<TableTemplate>> GetSystemPresetsForUpdateAsync(CancellationToken cancellationToken = default);
}
