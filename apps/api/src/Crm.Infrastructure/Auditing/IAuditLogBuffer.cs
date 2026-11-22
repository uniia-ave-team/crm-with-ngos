namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Represents an asynchronous, non-blocking buffer for dispatching <see cref="EntityAuditRecord"/> documents
/// to the configured <see cref="IAuditLogSink"/> without blocking business requests.
/// </summary>
public interface IAuditLogBuffer
{
    /// <summary>
    /// Attempts to enqueue a single audit record into the background dispatch buffer.
    /// </summary>
    /// <param name="record">The audit record to enqueue.</param>
    void Enqueue(EntityAuditRecord record);

    /// <summary>
    /// Enqueues a collection of audit records into the background dispatch buffer.
    /// </summary>
    /// <param name="records">The audit records to enqueue.</param>
    void EnqueueRange(IEnumerable<EntityAuditRecord> records);
}
