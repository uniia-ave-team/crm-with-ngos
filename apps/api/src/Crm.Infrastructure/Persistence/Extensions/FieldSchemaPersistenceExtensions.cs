using Crm.Domain.Entities;
using Crm.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides persistence extension methods for resolving unique index key collisions on <see cref="IEntityField"/> collections.
/// </summary>
public static class FieldSchemaPersistenceExtensions
{
    /// <summary>
    /// Resolves unique key constraint cycles when existing fields swap or chain-rename keys within the same parent schema.
    /// Temporarily updates colliding keys in the database to deterministic temporary keys and synchronizes
    /// the EF Core <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/> original values so the subsequent
    /// <see cref="DbContext.SaveChangesAsync(CancellationToken)"/> can apply the final keys in a single pass.
    /// </summary>
    /// <typeparam name="TField">The persisted field entity type.</typeparam>
    /// <param name="context">The active database context.</param>
    /// <param name="parentId">The unique identifier of the parent table or template.</param>
    /// <param name="parentIdSelector">A delegate selecting the parent identifier property from the field entity.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task ResolveFieldKeyCollisionsAsync<TField>(
        this DbContext context,
        Guid parentId,
        Func<TField, Guid> parentIdSelector,
        CancellationToken cancellationToken = default)
        where TField : BaseEntity, IEntityField
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(parentIdSelector);

        if (context.ChangeTracker.AutoDetectChangesEnabled)
        {
            context.ChangeTracker.DetectChanges();
        }

        var trackedEntries = context.ChangeTracker.Entries<TField>()
            .Where(e => parentIdSelector(e.Entity) == parentId)
            .ToList();

        var targetKeys = trackedEntries
            .Where(e => e.State != EntityState.Deleted)
            .Select(e => e.Entity.Key)
            .ToHashSet(StringComparer.Ordinal);

        var collidingEntries = trackedEntries
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted)
            .Where(e =>
                e.Property(nameof(IFieldSchema.Key)).OriginalValue is string oldKey
                && targetKeys.Contains(oldKey)
                && (e.State == EntityState.Deleted || !string.Equals(oldKey, e.Entity.Key, StringComparison.Ordinal)))
            .ToList();

        if (collidingEntries.Count == 0)
        {
            return;
        }

        foreach (var entry in collidingEntries)
        {
            var tempKey = $"__tmp_{entry.Entity.Id:N}";

            await context.Set<TField>()
                .Where(f => f.Id == entry.Entity.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(f => EF.Property<string>(f, nameof(IFieldSchema.Key)), tempKey),
                    cancellationToken);

            if (entry.State != EntityState.Deleted)
            {
                entry.Property(nameof(IFieldSchema.Key)).OriginalValue = tempKey;
            }
        }
    }
}
