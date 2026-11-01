using System.Linq.Expressions;
using System.Text.Json.Nodes;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Crm.Infrastructure.Persistence.Extensions.FunctionExtensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomRecord"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class CustomRecordRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomRecord>(appDbContext),
    ICustomRecordRepository
{
    /// <summary>
    /// Asynchronously determines whether any records exist for the specified custom table.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the custom table to check records for.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result is <see langword="true"/> if any records exist; otherwise, <see langword="false"/>.</returns>
    public Task<bool> AnyAsync(Guid customTableId, CancellationToken cancellationToken = default)
    {
        return DbSet
            .AsNoTracking()
            .AnyAsync(r => r.CustomTableId == customTableId, cancellationToken);
    }

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom records.
    /// Performs a full-text search across all JSON keys and values in the Data payload.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="customTableId">Filter to retrieve records belonging to a specific custom table.</param>
    /// <param name="searchTerm">Optional search term to filter records across all JSON payload fields.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="fieldDataType">Optional data type of the target dynamic field used for correct sorting conversion.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
    /// <param name="pageNumber">The current page number (1-based index).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated collection of projected custom record items.</returns>
    public Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid customTableId,
        string? searchTerm = null,
        string? orderBy = null,
        FieldDataType? fieldDataType = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.AsNoTracking()
            .FilterByTableAndSearch(customTableId, searchTerm)
            .OrderByCustomRecord(orderBy, sortOrder, fieldDataType);

        return ToPagedResultAsync<TResult>(query, pageNumber, pageSize, cancellationToken);
    }

    /// <summary>
    /// Asynchronously synchronizes the JSON payload structure for all records belonging to a specified custom table in a single atomic operation.
    /// Applies purge, two-phase key renames, and default value backfills.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the target custom table.</param>
    /// <param name="diff">The calculated schema difference containing fields to remove, rename, or seed.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the total number of modified records.</returns>
    public Task<int> SyncRecordSchemaAsync(Guid customTableId, SchemaDiffResult diff, CancellationToken cancellationToken = default)
    {
        var updateLambda = ((Expression<Func<CustomRecord, JsonObject>>)(r => r.Data)).ToSchemaUpdateLambda(diff);

        return updateLambda is null
            ? Task.FromResult(0)
            : DbSet
                .Where(r => r.CustomTableId == customTableId)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Data, updateLambda), cancellationToken);
    }

    /// <summary>
    /// Applies dynamic ordering to the query. Overrides the default repository implementation
    /// to support SQL-level sorting on JSONB keys within the <see cref="CustomRecord.Data"/> payload.
    /// </summary>
    /// <typeparam name="TResult">The projection target type.</typeparam>
    /// <param name="query">The input queryable sequence.</param>
    /// <param name="orderBy">The pre-resolved property name or exact JSONB key to sort by.</param>
    /// <param name="sortOrder">The sort direction specifier.</param>
    /// <returns>The sorted queryable sequence.</returns>
    protected override IQueryable<CustomRecord> ApplyOrdering<TResult>(
        IQueryable<CustomRecord> query,
        string orderBy,
        string? sortOrder)
        => ApplyCustomFieldOrdering<TResult>(query, orderBy, sortOrder, fieldDataType: null);

    /// <summary>
    /// Applies dynamic sorting to the query based on entity properties or dynamic JSONB keys within <see cref="CustomRecord.Data"/>.
    /// </summary>
    /// <typeparam name="TResult">The projection target type.</typeparam>
    /// <param name="query">The input <see cref="IQueryable{CustomRecord}"/> sequence to be sorted.</param>
    /// <param name="orderBy">The property name or JSONB key path (e.g., "name", "data.age", "data['age']").</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="fieldDataType">The optional target data type used to perform strongly-typed SQL casting for dynamic JSON values.</param>
    /// <returns>The sorted <see cref="IQueryable{CustomRecord}"/> sequence.</returns>
    protected virtual IQueryable<CustomRecord> ApplyCustomFieldOrdering<TResult>(
        IQueryable<CustomRecord> query,
        string orderBy,
        string? sortOrder,
        FieldDataType? fieldDataType)
        => query.OrderByCustomRecord(orderBy, sortOrder, fieldDataType);
}
