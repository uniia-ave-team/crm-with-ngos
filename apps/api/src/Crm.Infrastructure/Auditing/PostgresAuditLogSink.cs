using Crm.Infrastructure.DependencyInjection;
using Crm.Infrastructure.Entities;
using Crm.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Stage 1 PostgreSQL implementation of <see cref="IAuditLogSink"/> storing batches of
/// <see cref="EntityAuditRecord"/> documents into the standalone <c>AuditLogs</c> JSONB table.
/// Replace this single registration in <see cref="AuditingServiceCollectionExtensions"/> to switch
/// to a NoSQL store (MongoDB, OpenSearch, ClickHouse, DynamoDB) without touching any other code.
/// </summary>
/// <param name="scopeFactory">The DI scope factory used to create an isolated <see cref="ApplicationDbContext"/> per batch.</param>
public sealed class PostgresAuditLogSink(IServiceScopeFactory scopeFactory) : IAuditLogSink
{
    /// <inheritdoc />
    public async Task WriteBatchAsync(
        IReadOnlyCollection<EntityAuditRecord> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch.Count == 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

        var entries = new List<AuditLogEntry>(batch.Count);
        foreach (var record in batch)
        {
            entries.Add(record.ToAuditLogEntry());
        }

        await dbContext.Set<AuditLogEntry>().AddRangeAsync(entries, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
