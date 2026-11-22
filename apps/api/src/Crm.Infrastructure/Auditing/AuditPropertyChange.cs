using System.Text.Json.Nodes;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Represents a single property-level before/after value diff captured for an audited entity change.
/// </summary>
/// <param name="OldValue">The serialized value before the mutation, or <c>null</c> for creations.</param>
/// <param name="NewValue">The serialized value after the mutation, or <c>null</c> for deletions.</param>
public sealed record AuditPropertyChange(JsonNode? OldValue, JsonNode? NewValue);
