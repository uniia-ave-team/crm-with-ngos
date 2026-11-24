namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Defines the single pluggable storage sink contract for persisting batches of <see cref="EntityAuditRecord"/> documents.
/// Swap this registration in DI to migrate audit storage from PostgreSQL JSONB to MongoDB, OpenSearch, ClickHouse, or DynamoDB
/// without modifying domain entities, interceptors, or repositories.
/// </summary>
public interface IAuditLogSink
{
    /// <summary>
    /// Asynchronously persists a batch of entity audit records to the underlying log store.
    /// </summary>
    /// <param name="batch">The non-empty batch of audit records to persist.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    Task WriteBatchAsync(IReadOnlyCollection<EntityAuditRecord> batch, CancellationToken cancellationToken = default);
}
