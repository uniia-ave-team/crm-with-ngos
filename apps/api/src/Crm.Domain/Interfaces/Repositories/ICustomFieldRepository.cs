using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories.Generic;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Defines repository operations for managing <see cref="CustomField"/> entities.
/// </summary>
public interface ICustomFieldRepository : IGenericRepository<CustomField>
{
    /// <summary>
    /// Asynchronously retrieves all custom fields associated with a specific custom table ID, ordered by display order.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the parent custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="CustomField"/> entities.</returns>
    Task<IReadOnlyList<CustomField>> GetByCustomTableIdAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default);
}
