namespace Crm.Domain.Interfaces.Repositories.Generic;

/// <summary>
/// Defines pessimistic row-level locking operations for serializing concurrent updates within a transaction.
/// </summary>
public interface IRowLockRepository
{
    /// <summary>
    /// Asynchronously acquires a row-level write lock (<c>FOR UPDATE</c>) on the entity with the specified identifier
    /// within the active transaction.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to lock.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous lock operation.</returns>
    Task LockForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
}
