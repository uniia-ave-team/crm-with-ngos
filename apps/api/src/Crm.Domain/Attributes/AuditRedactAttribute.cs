namespace Crm.Domain.Attributes;

/// <summary>
/// Marks a sensitive property on an <see cref="AuditableAttribute"/>-decorated entity so that changes are still
/// recorded in the audit log, but raw before/after values are replaced with a redacted placeholder.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditRedactAttribute : Attribute
{
}
