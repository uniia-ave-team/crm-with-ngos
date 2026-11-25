using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Repositories;

/// <summary>
/// Implements the repository for managing global <see cref="UserCustomField"/> schema entities.
/// </summary>
/// <param name="appDbContext">The application database context.</param>
public sealed class UserCustomFieldRepository(
    ApplicationDbContext appDbContext)
    : GenericRepository<UserCustomField>(appDbContext),
    IUserCustomFieldRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UserCustomField>> GetAllOrderedAsync(CancellationToken cancellationToken = default)
        => await DbSet
            .AsNoTracking()
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<List<UserCustomField>> GetAllForUpdateAsync(CancellationToken cancellationToken = default)
        => DbSet
            .OrderBy(f => f.Order)
            .ThenBy(f => f.Id)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ApplySchemaChangesAsync(
        IEnumerable<UserCustomField> addedFields,
        IEnumerable<UserCustomField> removedFields,
        CancellationToken cancellationToken = default)
    {
        var removed = removedFields as IReadOnlyCollection<UserCustomField> ?? [.. removedFields];
        if (removed.Count > 0)
        {
            DbSet.RemoveRange(removed);
        }

        var added = addedFields as IReadOnlyCollection<UserCustomField> ?? [.. addedFields];
        if (added.Count > 0)
        {
            await DbSet.AddRangeAsync(added, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task ResolveFieldKeyCollisionsAsync(CancellationToken cancellationToken = default)
        => Context.ResolveFieldKeyCollisionsAsync<UserCustomField>(cancellationToken);
}
