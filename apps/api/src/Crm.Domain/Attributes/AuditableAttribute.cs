namespace Crm.Domain.Attributes;

/// <summary>
/// Marks a domain entity class for automatic change-history auditing.
/// Only entities decorated with this attribute are recorded in the centralized entity audit log.
/// </summary>
/// <param name="entityName">
/// Optional logical entity name stored in the audit log; defaults to the CLR type name when omitted.
/// </param>
[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class AuditableAttribute(string? entityName = null) : Attribute
{
    /// <summary>
    /// Gets the optional custom logical entity name used in audit records.
    /// </summary>
    public string? EntityName { get; } = string.IsNullOrWhiteSpace(entityName) ? null : entityName.Trim();
}
