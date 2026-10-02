namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines command operations for modifying the database state.
/// </summary>
public interface ICommandRepository<T>
    where T : IEntity
{
    /// <summary>
    /// Asynchronously creates a new entity in the underlying data store.
    /// </summary>
    /// <param name="entity">The entity to create.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the newly created entity.</returns>
    Task<T> CreateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously deletes an entity from the underlying data store by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous deletion operation.</returns>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
