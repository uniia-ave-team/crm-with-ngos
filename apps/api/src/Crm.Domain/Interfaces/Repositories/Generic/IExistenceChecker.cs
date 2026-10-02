using Crm.Domain.Exceptions;

namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines lightweight validation operations to check entity existence without tracking.
/// </summary>
public interface IExistenceChecker
{
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
}
