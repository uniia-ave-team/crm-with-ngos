using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Provides non-intrusive <see cref="DbContext"/> extension methods for recording audit entries after
/// direct bulk SQL mutations (<c>ExecuteDeleteAsync</c> / <c>ExecuteUpdateAsync</c>) by delegating to the
/// <see cref="EntityAuditSaveChangesInterceptor"/> attached to the context, keeping <see cref="DbContext"/>
/// subclasses completely decoupled from auditing infrastructure.
/// </summary>
internal static class DbContextAuditExtensions
{
    /// <summary>
    /// Enqueues a <see cref="AuditAction.Deleted"/> audit record for a direct bulk SQL deletion
    /// when <typeparamref name="TEntity"/> is decorated with <see cref="Domain.Attributes.AuditableAttribute"/>
    /// and an <see cref="EntityAuditSaveChangesInterceptor"/> is attached to <paramref name="context"/>.
    /// </summary>
    /// <typeparam name="TEntity">The deleted entity type.</typeparam>
    /// <param name="context">The active database context.</param>
    /// <param name="entityKey">The stringified primary key of the deleted entity.</param>
    public static void TryEnqueueBulkDeleteAudit<TEntity>(this DbContext context, string entityKey)
        where TEntity : class
        => ResolveInterceptor(context)?.RecordBulkDelete<TEntity>(context, entityKey);

    /// <summary>
    /// Enqueues an <see cref="AuditAction.Updated"/> audit record for a direct bulk SQL update
    /// when <typeparamref name="TEntity"/> is decorated with <see cref="Domain.Attributes.AuditableAttribute"/>
    /// and an <see cref="EntityAuditSaveChangesInterceptor"/> is attached to <paramref name="context"/>.
    /// </summary>
    /// <typeparam name="TEntity">The updated entity type.</typeparam>
    /// <param name="context">The active database context.</param>
    /// <param name="entityKey">The stringified primary key of the updated entity.</param>
    /// <param name="changes">The property-level before/after diffs applied by the bulk update.</param>
    public static void TryEnqueueBulkUpdateAudit<TEntity>(
        this DbContext context,
        string entityKey,
        IReadOnlyDictionary<string, AuditPropertyChange> changes)
        where TEntity : class
        => ResolveInterceptor(context)?.RecordBulkUpdate<TEntity>(context, entityKey, changes);

    private static EntityAuditSaveChangesInterceptor? ResolveInterceptor(DbContext context)
        => context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()
            ?.Interceptors
            ?.OfType<EntityAuditSaveChangesInterceptor>()
            .FirstOrDefault();
}
