using System.Linq.Expressions;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines a generic repository interface for performing standard CRUD operations
/// on entities of a specific type.
/// </summary>
/// <typeparam name="T">The type of the entity. Must implement <see cref="IEntity"/>.</typeparam>
public interface IGenericRepository<T>
    where T : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a single entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved entity.</returns>
    Task<T> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves and projects an entity by its unique identifier.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element (e.g., DTO).</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="selector">Projection expression to transform the entity into a DTO.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>The projected entity DTO with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    Task<TResult> GetAsync<TResult>(Guid id, Expression<Func<T, TResult>> selector, CancellationToken ct = default);

    /// <summary>
    /// Asynchronously checks if an entity with the specified unique identifier exists.
    /// Throws an exception if the entity is not found.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if no entity with the specified ID exists.</exception>
    Task EnsureExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously ensures that entities exist for all specified unique identifiers.
    /// Throws an exception containing all missing keys if any entity is not found.
    /// </summary>
    /// <param name="ids">The collection of unique identifiers to check.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="EntitiesNotFoundException">Thrown if any entity with the specified ID is not found.</exception>
    Task EnsureAllExistAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a complete list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="ct">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of entities.</returns>
    Task<List<T>> GetListAsync(CancellationToken ct = default);

    /// <summary>
    /// Asynchronously retrieves a paginated, filtered, and sorted list of projected entities.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected elements (e.g., DTO).</typeparam>
    /// <param name="selector">Projection expression to transform entities into DTOs directly in SQL.</param>
    /// <param name="predicate">Optional expression to filter the entities.</param>
    /// <param name="orderBy">The name of the property to sort by.</param>
    /// <param name="sortOrder">The sort direction ("asc" or "desc").</param>
    /// <param name="pageNumber">The current page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="ct">A token to monitor for cancellation requests.</param>
    /// <returns>A paginated result containing the projected items and metadata.</returns>
    Task<PagedResult<TResult>> GetPagedAsync<TResult>(
        Expression<Func<T, TResult>> selector,
        Expression<Func<T, bool>>? predicate = null,
        string? orderBy = null,
        string? sortOrder = SortOrderConstants.Ascending,
        int pageNumber = 1,
        int pageSize = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Asynchronously creates a new entity in the underlying data store.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the newly created entity.</returns>
    Task<T> CreateAsync(T entity, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing entity in the underlying data store's tracking state.
    /// </summary>
    /// <param name="entity">The entity containing the updated data.</param>
    /// <returns>The updated entity.</returns>
    T Update(T entity);

    /// <summary>
    /// Asynchronously deletes an entity from the underlying data store by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="ct">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
