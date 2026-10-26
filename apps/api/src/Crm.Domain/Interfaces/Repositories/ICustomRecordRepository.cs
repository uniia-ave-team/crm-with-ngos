using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="CustomRecord"/> entities and their JSON payloads.
/// </summary>
public interface ICustomRecordRepository : IGenericRepository<CustomRecord>
{
    /// <summary>
    /// Asynchronously determines whether any records exist for the specified custom table.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the custom table to check records for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result is <see langword="true"/> if any records exist; otherwise, <see langword="false"/>.</returns>
    Task<bool> AnyAsync(Guid customTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom records.
    /// Performs a full-text search across all JSON keys and values in the Data payload.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="customTableId">Filter to retrieve records belonging to a specific custom table.</param>
    /// <param name="searchTerm">Optional search term to filter records across all JSON payload fields.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected custom record items.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid customTableId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously synchronizes the JSON payload structure for all records belonging to a specified custom table in a single atomic operation.
    /// Applies purge, two-phase key renames, and default value backfills.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the target custom table.</param>
    /// <param name="diff">The calculated schema difference containing fields to remove, rename, or seed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the total number of modified records.</returns>
    Task<int> SyncRecordSchemaAsync(Guid customTableId, SchemaDiffResult diff, CancellationToken cancellationToken = default);
}
