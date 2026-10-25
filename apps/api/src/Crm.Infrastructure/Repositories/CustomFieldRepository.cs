using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomField"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public class CustomFieldRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomField>(appDbContext),
    ICustomFieldRepository
{
    /// <summary>
    /// Asynchronously retrieves all custom fields associated with a specific custom table ID.
    /// </summary>
    /// <param name="customTableId">The unique identifier of the parent custom table.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of <see cref="CustomField"/> entities.</returns>
    public async Task<IReadOnlyList<CustomField>> GetByCustomTableIdAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.CustomTableId == customTableId)
            .ToListAsync(cancellationToken);
    }
}
