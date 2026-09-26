using Crm.Domain.Exceptions;

namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines read-only operations for retrieving entities.
/// </summary>
public interface IQueryRepository<T>
    where T : IEntity
{
    /// <summary>
    /// Asynchronously retrieves a single entity by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved entity.</returns>
    Task<T> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves an entity by its unique identifier with change tracking enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The tracked entity with the specified ID.</returns>
    /// <exception cref="EntityNotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    Task<T> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves a complete list of entities of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the underlying operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a list of entities.</returns>
    Task<List<T>> GetListAsync(CancellationToken cancellationToken = default);
}
