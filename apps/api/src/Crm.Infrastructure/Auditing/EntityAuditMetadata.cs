using System.Collections.Frozen;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Represents cached attribute-driven auditing metadata for a single auditable entity type.
/// </summary>
/// <param name="EntityName">The logical entity name written to audit records.</param>
/// <param name="IgnoredProperties">The set of property names excluded from change tracking.</param>
/// <param name="RedactedProperties">The set of property names whose before/after values are masked.</param>
internal sealed record EntityAuditMetadata(
    string EntityName,
    FrozenSet<string> IgnoredProperties,
    FrozenSet<string> RedactedProperties);
