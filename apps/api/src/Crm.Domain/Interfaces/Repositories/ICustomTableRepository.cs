using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing <see cref="CustomTable"/> entities and retrieving their ordered <see cref="CustomField"/> schemas.
/// </summary>
public interface ICustomTableRepository : IGenericRepository<CustomTable>
{
    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected custom tables belonging to a specific NGO.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., summary DTO).</typeparam>
    /// <param name="ngoId">The unique identifier of the NGO that owns the custom tables.</param>
    /// <param name="searchTerm">Optional search keyword applied to table names and descriptions.</param>
    /// <param name="orderBy">The property name to sort the results by.</param>
    /// <param name="sortOrder">The sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
    /// <param name="pageNumber">The 1-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the matching projected custom tables.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Guid ngoId,
        string? searchTerm = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = PaginationConstants.MinPageNumber,
        int pageSize = PaginationConstants.DefaultPageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a pessimistic row-level write lock (<c>FOR NO KEY UPDATE</c>) on the custom table row
    /// within the active transaction to serialize concurrent schema or record position mutations.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table to lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous lock operation.</returns>
    Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a tracked <see cref="CustomTable"/> entity with its <see cref="CustomTable.Fields"/> collection loaded for modification.
    /// </summary>
    /// <param name="id">The unique identifier of the custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked custom table entity with its fields.</returns>
    Task<CustomTable> GetForUpdateWithFieldsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all <see cref="CustomField"/> definitions belonging to a specific custom table,
    /// ordered by <see cref="Domain.Entities.Base.FieldDefinitionBase.Order"/>.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the parent custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only ordered list of custom field definitions.</returns>
    Task<IReadOnlyList<CustomField>> GetFieldsByCustomTableIdAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Explicitly registers newly added and removed <see cref="CustomField"/> entities in the persistence context
    /// so client-generated field identifiers are always tracked as inserts rather than updates.
    /// </summary>
    /// <param name="customTableId">The parent custom table identifier.</param>
    /// <param name="addedFields">Newly created custom fields to insert.</param>
    /// <param name="removedFields">Removed custom fields to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous staging operation.</returns>
    Task ApplyFieldSchemaChangesAsync(
        Guid customTableId,
        IEnumerable<CustomField> addedFields,
        IEnumerable<CustomField> removedFields,
        CancellationToken cancellationToken = default);
}
