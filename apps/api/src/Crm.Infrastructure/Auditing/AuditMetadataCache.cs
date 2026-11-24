using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using Crm.Domain.Attributes;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Caches resolved <see cref="AuditableAttribute"/>, <see cref="AuditIgnoreAttribute"/>, and
/// <see cref="AuditRedactAttribute"/> metadata per CLR entity type to avoid repeated reflection overhead.
/// </summary>
internal static class AuditMetadataCache
{
    private static readonly ConcurrentDictionary<Type, EntityAuditMetadata?> Cache = new();

    /// <summary>
    /// Returns the cached audit metadata for <paramref name="entityType"/>, or <c>null</c> if the type
    /// is not decorated with <see cref="AuditableAttribute"/>.
    /// </summary>
    /// <param name="entityType">The CLR entity type to inspect.</param>
    /// <returns>The resolved <see cref="EntityAuditMetadata"/>, or <c>null</c> when auditing is not enabled for the type.</returns>
    public static EntityAuditMetadata? TryGetMetadata(Type entityType)
        => Cache.GetOrAdd(entityType, BuildMetadata);

    private static EntityAuditMetadata? BuildMetadata(Type entityType)
    {
        var auditableAttribute = entityType.GetCustomAttribute<AuditableAttribute>(inherit: true);
        if (auditableAttribute is null)
        {
            return null;
        }

        var ignored = new HashSet<string>(StringComparer.Ordinal);
        var redacted = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.IsDefined(typeof(AuditIgnoreAttribute), inherit: true))
            {
                ignored.Add(property.Name);
            }
            else if (property.IsDefined(typeof(AuditRedactAttribute), inherit: true))
            {
                redacted.Add(property.Name);
            }
        }

        string entityName = auditableAttribute.EntityName ?? entityType.Name;

        return new EntityAuditMetadata(
            entityName,
            ignored.ToFrozenSet(StringComparer.Ordinal),
            redacted.ToFrozenSet(StringComparer.Ordinal));
    }
}
