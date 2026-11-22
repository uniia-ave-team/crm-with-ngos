using System.Linq.Expressions;
using Crm.Domain.Entities;
using Crm.Domain.Interfaces.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Extensions;

/// <summary>
/// Provides reusable <see cref="EntityTypeBuilder{TEntity}"/> configuration helpers for
/// <see cref="IAuthorTrackedEntity"/> and <see cref="ISoftDeletableEntity"/> mappings.
/// </summary>
internal static class EntityTypeBuilderAuditExtensions
{
    /// <summary>
    /// Configures optional <see cref="IAuthorTrackedEntity.CreatedBy"/> and <see cref="IAuthorTrackedEntity.UpdatedBy"/>
    /// foreign keys to <see cref="User"/> with <see cref="DeleteBehavior.SetNull"/> and indexes.
    /// </summary>
    /// <typeparam name="TEntity">The configured entity type.</typeparam>
    /// <param name="builder">The EF Core entity type builder.</param>
    /// <param name="creatorNavigation">The navigation expression for the creator user.</param>
    /// <param name="updaterNavigation">The navigation expression for the updater user.</param>
    public static void ConfigureAuthorTracking<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, User?>> creatorNavigation,
        Expression<Func<TEntity, User?>> updaterNavigation)
        where TEntity : class, IAuthorTrackedEntity
    {
        builder.HasOne(creatorNavigation)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(updaterNavigation)
            .WithMany()
            .HasForeignKey(e => e.UpdatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.CreatedBy);
        builder.HasIndex(e => e.UpdatedBy);
    }

    /// <summary>
    /// Configures <see cref="ISoftDeletableEntity.IsDeleted"/> (default <c>false</c>), an index for soft-delete filtering,
    /// and a global query filter excluding soft-deleted records by default.
    /// </summary>
    /// <typeparam name="TEntity">The configured entity type.</typeparam>
    /// <param name="builder">The EF Core entity type builder.</param>
    public static void ConfigureSoftDelete<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : class, ISoftDeletableEntity
    {
        builder.Property(e => e.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasIndex(e => e.IsDeleted);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
