namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Represents a self-contained, NoSQL-ready audit document describing a single entity lifecycle mutation.
/// Intentionally carries no relational foreign keys so it can be persisted to PostgreSQL JSONB today
/// and migrated seamlessly to MongoDB, OpenSearch, ClickHouse, or DynamoDB without changing domain or interceptor code.
/// </summary>
/// <param name="Id">The time-ordered unique identifier (UUID v7) of the audit record.</param>
/// <param name="OccurredAtUtc">The UTC timestamp when the change occurred.</param>
/// <param name="ActorUserId">The unique identifier of the user who performed the change, or <c>null</c> for system/CLI operations.</param>
/// <param name="TraceId">The optional distributed trace or HTTP request identifier for correlation.</param>
/// <param name="EntityType">The logical entity type name (e.g., <c>"ProjectTask"</c>, <c>"CustomRecord"</c>).</param>
/// <param name="EntityKey">The stringified primary key of the entity (single GUID or composite <c>"key1:key2"</c>).</param>
/// <param name="Action">The lifecycle mutation action (<see cref="AuditAction"/>).</param>
/// <param name="Changes">The dictionary of changed property paths mapped to their before/after values.</param>
public sealed record EntityAuditRecord(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid? ActorUserId,
    string? TraceId,
    string EntityType,
    string EntityKey,
    AuditAction Action,
    IReadOnlyDictionary<string, AuditPropertyChange> Changes);
