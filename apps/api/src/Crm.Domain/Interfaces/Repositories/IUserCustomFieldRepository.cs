using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for managing global <see cref="UserCustomField"/> schema definitions.
/// </summary>
public interface IUserCustomFieldRepository : IGenericRepository<UserCustomField>
{
    /// <summary>
    /// Retrieves all global user custom field definitions ordered by <see cref="Domain.Entities.Base.FieldDefinitionBase.Order"/> and <see cref="Domain.Entities.Base.BaseEntity.Id"/> without tracking.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An ordered read-only list of all global user custom fields.</returns>
    Task<IReadOnlyList<UserCustomField>> GetAllOrderedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all tracked global user custom field definitions for transactional schema synchronization.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tracked list of all global user custom fields.</returns>
    Task<List<UserCustomField>> GetAllForUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages newly added and removed <see cref="UserCustomField"/> entities on the underlying database context.
    /// </summary>
    /// <param name="addedFields">Newly created user custom field definitions to add.</param>
    /// <param name="removedFields">Removed user custom field definitions to delete.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplySchemaChangesAsync(
        IEnumerable<UserCustomField> addedFields,
        IEnumerable<UserCustomField> removedFields,
        CancellationToken cancellationToken = default);
}
