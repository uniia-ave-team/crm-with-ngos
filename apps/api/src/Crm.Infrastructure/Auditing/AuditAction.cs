namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Represents the lifecycle mutation action captured in an entity audit log record.
/// </summary>
public enum AuditAction : byte
{
    /// <summary>A new entity instance was created.</summary>
    Created = 1,

    /// <summary>One or more tracked properties of an existing entity were modified.</summary>
    Updated = 2,

    /// <summary>An existing entity instance was deleted.</summary>
    Deleted = 3,
}
