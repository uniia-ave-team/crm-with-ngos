namespace Crm.Domain.Attributes;

/// <summary>
/// Excludes a property on an <see cref="AuditableAttribute"/>-decorated entity from being recorded in the audit log
/// (for example, derived normalization columns or technical concurrency tokens).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class AuditIgnoreAttribute : Attribute
{
}
