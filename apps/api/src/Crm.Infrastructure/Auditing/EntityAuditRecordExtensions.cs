using System.Text.Json.Nodes;
using Crm.Infrastructure.Entities;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Provides mapping extension methods for <see cref="EntityAuditRecord"/> documents.
/// </summary>
public static class EntityAuditRecordExtensions
{
    /// <summary>
    /// Maps an immutable <see cref="EntityAuditRecord"/> document to a persistence <see cref="AuditLogEntry"/>.
    /// </summary>
    /// <param name="record">The source audit record.</param>
    /// <returns>A new <see cref="AuditLogEntry"/> ready for storage.</returns>
    public static AuditLogEntry ToAuditLogEntry(this EntityAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var changesJson = new JsonObject();
        foreach (var (propertyPath, change) in record.Changes)
        {
            changesJson[propertyPath] = new JsonObject
            {
                ["old"] = change.OldValue?.DeepClone(),
                ["new"] = change.NewValue?.DeepClone(),
            };
        }

        return new AuditLogEntry
        {
            Id = record.Id,
            OccurredAtUtc = record.OccurredAtUtc,
            ActorUserId = record.ActorUserId,
            TraceId = record.TraceId,
            EntityType = record.EntityType,
            EntityKey = record.EntityKey,
            Action = record.Action,
            Changes = changesJson,
        };
    }
}
