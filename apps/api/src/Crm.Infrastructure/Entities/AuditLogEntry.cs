using System.Text.Json.Nodes;
using Crm.Infrastructure.Auditing;

namespace Crm.Infrastructure.Entities;

/// <summary>
/// Stage 1 PostgreSQL persistence document for <see cref="EntityAuditRecord"/>.
/// Deliberately NOT decorated with <see cref="Domain.Attributes.AuditableAttribute"/> and holds NO foreign keys
/// to domain tables so it can be archived, truncated, or replaced by a NoSQL sink without relational coupling.
/// </summary>
public sealed class AuditLogEntry
{
    /// <summary>
    /// Gets the time-ordered unique identifier (UUID v7) of the audit log entry.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the audited entity mutation occurred.
    /// </summary>
    public DateTimeOffset OccurredAtUtc { get; init; }

    /// <summary>
    /// Gets the unique identifier of the user who performed the mutation, or <c>null</c> for system/CLI operations.
    /// </summary>
    public Guid? ActorUserId { get; init; }

    /// <summary>
    /// Gets the optional distributed trace or HTTP request correlation identifier.
    /// </summary>
    public string? TraceId { get; init; }

    /// <summary>
    /// Gets the logical entity type name (e.g., <c>"ProjectTask"</c>, <c>"CustomRecord"</c>).
    /// </summary>
    public required string EntityType { get; init; }

    /// <summary>
    /// Gets the stringified primary key of the mutated entity (single GUID or composite <c>"key1:key2"</c>).
    /// </summary>
    public required string EntityKey { get; init; }

    /// <summary>
    /// Gets the lifecycle action performed on the entity.
    /// </summary>
    public AuditAction Action { get; init; }

    /// <summary>
    /// Gets the JSONB document containing property-level before/after diffs (<c>{ "Property": { "old": ..., "new": ... } }</c>).
    /// </summary>
    public required JsonObject Changes { get; init; }
}
