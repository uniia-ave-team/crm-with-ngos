using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines the contract for the repository managing global <see cref="UserCustomField"/> schema definitions.
/// </summary>
public interface IUserCustomFieldRepository : IGenericRepository<UserCustomField>
{
    /// <summary>
    /// Asynchronously retrieves all <see cref="UserCustomField"/> definitions ordered by <see cref="IOrderedEntity.Order"/>
    /// without change tracking.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered read-only list of all user custom field schema definitions.</returns>
    Task<IReadOnlyList<UserCustomField>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves all tracked <see cref="UserCustomField"/> definitions ordered by <see cref="IOrderedEntity.Order"/>
    /// for atomic schema synchronization.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A mutable list of tracked user custom field entities.</returns>
    Task<List<UserCustomField>> GetAllForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages newly added and removed <see cref="UserCustomField"/> entities in the EF Core change tracker
    /// following schema reconciliation.
    /// </summary>
    /// <param name="addedFields">The new field definitions appended to the schema.</param>
    /// <param name="removedFields">The field definitions removed from the schema.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous staging operation.</returns>
    Task ApplySchemaChangesAsync(
        IEnumerable<UserCustomField> addedFields,
        IEnumerable<UserCustomField> removedFields,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves unique key constraint collisions when existing <see cref="UserCustomField"/> definitions swap or chain-rename keys
    /// within a single schema synchronization pass.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous collision resolution operation.</returns>
    Task ResolveFieldKeyCollisionsAsync(CancellationToken cancellationToken = default);
}
