using System.Collections.Frozen;
using Crm.Domain.Enums;

namespace Crm.Domain.Extensions;

/// <summary>
/// Provides extension methods for converting <see cref="AccessRight"/> values into permission claim strings.
/// </summary>
public static class PermissionExtensions
{
    private const string PermissionPrefix = "Permissions";

    private static readonly FrozenSet<string> _allPermissions =
        Enum.GetValues<AccessRight>()
            .Select(p => $"{PermissionPrefix}.{p}")
            .ToFrozenSet();

    /// <summary>
    /// Converts a permission enum to its full string claim representation (e.g., "Permissions.CreateProject").
    /// </summary>
    /// <param name="permission">The access right permission to convert.</param>
    /// <returns>The prefixed permission claim value.</returns>
    public static string ToClaimValue(this AccessRight permission) => $"{PermissionPrefix}.{permission}";

    /// <summary>
    /// Returns all permissions as string values for Identity / Claims assignment.
    /// </summary>
    /// <returns>An immutable set containing all canonical permission claim values.</returns>
    public static IReadOnlySet<string> GetAllStringValues() => _allPermissions;
}
