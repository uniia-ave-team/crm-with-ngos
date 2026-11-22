using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines a generic repository interface for performing standard CRUD, projection,
/// and existence-check operations on entities of a specific type.
/// </summary>
/// <typeparam name="T">The type of the entity. Must implement <see cref="IEntity"/>.</typeparam>
public interface IGenericRepository<T> :
    IProjectingRepository,
    IExistenceChecker
    where T : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a single entity by its unique identifier without change tracking.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved entity.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    Task<T> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked entity with the specified ID.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    Task<T> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously creates a new entity in the underlying data store.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the newly created entity.</returns>
    Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds a collection of entities to the database context.
    /// </summary>
    /// <param name="entities">The collection of entities to create.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    Task CreateAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an entity from the underlying data store by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
