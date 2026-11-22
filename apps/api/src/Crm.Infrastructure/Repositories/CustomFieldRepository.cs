using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Provides repository operations for managing <see cref="CustomField"/> entities in the database.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class CustomFieldRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<CustomField>(appDbContext),
    ICustomFieldRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomField>> GetByCustomTableIdAsync(
        Guid customTableId,
        CancellationToken cancellationToken = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(x => x.CustomTableId == customTableId)
            .OrderBy(x => x.Order)
            .ToListAsync(cancellationToken);
    }
}
