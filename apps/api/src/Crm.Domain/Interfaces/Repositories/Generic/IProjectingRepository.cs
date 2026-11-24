using Crm.Domain.Exceptions;

namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines read-only operations that project a single entity directly into a target type.
/// </summary>
public interface IProjectingRepository
{
    /// <summary>
    /// Asynchronously retrieves and projects an entity by its unique identifier.
    /// </summary>
    /// <typeparam name="TResult">The type of the projected element.</typeparam>
    /// <param name="id">The unique identifier of the entity to retrieve.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The projected result with the specified ID.</returns>
    /// <exception cref="NotFoundException">Thrown if the entity with the specified ID is not found.</exception>
    Task<TResult> GetAsync<TResult>(Guid id, CancellationToken cancellationToken = default);
}
