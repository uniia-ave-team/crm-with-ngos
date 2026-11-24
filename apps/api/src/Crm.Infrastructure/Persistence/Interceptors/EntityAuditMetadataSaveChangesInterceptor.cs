using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Entities;
using Crm.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core <see cref="SaveChangesInterceptor"/> that automatically populates UTC+0 lifecycle timestamps
/// (<see cref="ICreatableEntity.CreatedAt"/>, <see cref="IAuditableEntity.UpdatedAt"/>, <see cref="ProjectMember.JoinedAt"/>),
/// actor identifiers (<see cref="IAuthorTrackedEntity.CreatedBy"/>, <see cref="IAuthorTrackedEntity.UpdatedBy"/>),
/// and converts physical deletions of <see cref="ISoftDeletableEntity"/> instances into soft deletes
/// before changes are persisted.
/// </summary>
/// <param name="timeProvider">The time provider supplying the current UTC+0 timestamp.</param>
/// <param name="actorAccessor">Optional actor accessor resolving the authenticated user identifier.</param>
public sealed class EntityAuditMetadataSaveChangesInterceptor(
    TimeProvider timeProvider,
    IAuditActorAccessor? actorAccessor = null) : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyMetadata(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyMetadata(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyMetadata(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Guid? actorUserId = null;
        bool actorResolved = false;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Deleted && entry.Entity is ISoftDeletableEntity softDeletable)
            {
                entry.State = EntityState.Modified;
                softDeletable.IsDeleted = true;
                softDeletable.DeletedAt = utcNow;

                if (entry.Entity is IAuditableEntity softDeletedAuditable)
                {
                    softDeletedAuditable.UpdatedAt = utcNow;
                }

                if (entry.Entity is IAuthorTrackedEntity softDeletedAuthorTracked)
                {
                    Guid? actor = ResolveActor(ref actorResolved, ref actorUserId);
                    if (actor is not null)
                    {
                        softDeletedAuthorTracked.UpdatedBy = actor;
                    }
                }

                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is ICreatableEntity creatable && creatable.CreatedAt == default)
                {
                    creatable.CreatedAt = utcNow;
                }

                if (entry.Entity is IAuditableEntity auditable && auditable.UpdatedAt == default)
                {
                    auditable.UpdatedAt = utcNow;
                }

                if (entry.Entity is ProjectMember member && member.JoinedAt == default)
                {
                    member.JoinedAt = utcNow;
                }

                if (entry.Entity is IAuthorTrackedEntity authorTracked)
                {
                    Guid? actor = ResolveActor(ref actorResolved, ref actorUserId);
                    if (actor is not null)
                    {
                        authorTracked.CreatedBy ??= actor;
                        authorTracked.UpdatedBy ??= actor;
                    }
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is IAuditableEntity auditable)
                {
                    var updatedAtProperty = entry.Property(nameof(IAuditableEntity.UpdatedAt));
                    if (!updatedAtProperty.IsModified || Equals(updatedAtProperty.OriginalValue, updatedAtProperty.CurrentValue))
                    {
                        auditable.UpdatedAt = utcNow;
                    }
                }

                if (entry.Entity is IAuthorTrackedEntity authorTracked)
                {
                    Guid? actor = ResolveActor(ref actorResolved, ref actorUserId);
                    if (actor is not null)
                    {
                        authorTracked.UpdatedBy = actor;
                    }
                }
            }
        }
    }

    private Guid? ResolveActor(ref bool resolved, ref Guid? cachedActorId)
    {
        if (!resolved)
        {
            cachedActorId = actorAccessor?.GetActorUserId();
            resolved = true;
        }

        return cachedActorId;
    }
}
