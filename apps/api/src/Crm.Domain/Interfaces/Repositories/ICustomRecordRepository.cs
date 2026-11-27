using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing <see cref="CustomRecord"/> entities, including fractional-indexing
/// manual positioning, smart dynamic sorting, full-text search, and bulk JSONB schema synchronization.
/// </summary>
public interface ICustomRecordRepository : IGenericRepository<CustomRecord>
{
    /// <summary>
    /// Checks asynchronously whether any records currently exist in the specified custom table.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if at least one record exists in the table; otherwise, <see langword="false"/>.</returns>
    Task<bool> AnyAsync(Guid customTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom records for a specific table.
    /// When <paramref name="orderBy"/> is <see langword="null"/> or blank, records are returned in manual fractional-index order.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="customTableId">The unique identifier of the parent custom table.</param>
    /// <param name="searchTerm">Optional search keyword matched against JSONB string values via full-text search.</param>
    /// <param name="orderBy">Optional entity property name or canonical custom field key; <see langword="null"/> uses manual position order.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the matching projected custom records.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid customTableId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves lightweight position metadata for a single custom record without loading its JSONB payload.
    /// </summary>
    /// <param name="id">The unique identifier of the custom record.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The position metadata if found; otherwise, <see langword="null"/>.</returns>
    Task<CustomRecordPositionInfo?> GetPositionInfoAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves lightweight position metadata for a set of record identifiers scoped to a specific custom table.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="ids">The record identifiers to look up.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only dictionary mapping found record identifiers to their position metadata.</returns>
    Task<IReadOnlyDictionary<Guid, CustomRecordPositionInfo>> GetPositionInfosByIdsAsync(
        Guid customTableId,
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the highest (tail) fractional position rank currently present in the specified custom table.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The current tail position rank, or <see langword="null"/> if the table is empty.</returns>
    Task<string?> GetLastPositionAsync(Guid customTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any record (excluding <paramref name="excludeRecordId"/>) exists strictly within the open position interval
    /// defined by optional <paramref name="exclusiveLowerBound"/> and <paramref name="exclusiveUpperBound"/>.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="exclusiveLowerBound">Optional exclusive lower bound position (<c>Position &gt; lower</c>).</param>
    /// <param name="exclusiveUpperBound">Optional exclusive upper bound position (<c>Position &lt; upper</c>).</param>
    /// <param name="excludeRecordId">Optional moving record identifier to ignore.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if another record lies strictly inside the specified interval; otherwise, <see langword="false"/>.</returns>
    Task<bool> HasRecordInPositionRangeAsync(
        Guid customTableId,
        string? exclusiveLowerBound = null,
        string? exclusiveUpperBound = null,
        Guid? excludeRecordId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates only <see cref="CustomRecord.Position"/> directly in the database without modifying audit timestamps or authors.
    /// </summary>
    /// <param name="id">The unique identifier of the record to reposition.</param>
    /// <param name="position">The new fractional position rank.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous update.</returns>
    Task UpdatePositionAsync(Guid id, string position, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all record identifiers in the specified custom table ordered by their current manual <see cref="CustomRecord.Position"/>.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered list of record identifiers.</returns>
    Task<IReadOnlyList<Guid>> GetOrderedIdsAsync(Guid customTableId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all <see cref="CustomRecord.Position"/> values in the specified table using set-based EF Core bulk updates
    /// without modifying audit timestamps or authors.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the owning custom table.</param>
    /// <param name="positionsById">Mapping of every record identifier in the table to its new unique fractional position rank.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous bulk replacement.</returns>
    Task ReplaceAllPositionsAsync(
        Guid customTableId,
        IReadOnlyDictionary<Guid, string> positionsById,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes JSONB payloads for all records in a custom table directly in the database
    /// by dropping removed keys and backfilling newly required keys in a single atomic statement.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the custom table.</param>
    /// <param name="plan">The computed schema synchronization plan.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of record rows updated in the database.</returns>
    Task<int> SyncRecordSchemaAsync(Guid customTableId, SchemaSyncPlan plan, CancellationToken cancellationToken = default);
}
