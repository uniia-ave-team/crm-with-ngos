using System.Collections.Frozen;
using Crm.Domain.Enums;

namespace Crm.Domain.Extensions;

public static class PermissionExtensions
{
    public const string ClaimType = "Permission";

    private const string PermissionPrefix = "Permissions";

    private static readonly FrozenSet<string> _allPermissions =
        Enum.GetValues<AccessRight>()
            .Select(p => $"{PermissionPrefix}.{p}")
            .ToFrozenSet();

    /// <summary>
    /// Converts a permission enum to its full string claim representation (e.g., "Permissions.CreateProject").
    /// </summary>
    public static string ToClaimValue(this AccessRight permission) => $"{PermissionPrefix}.{permission}";

    /// <summary>
    /// Returns all permissions as string values for Identity / Claims assignment.
    /// </summary>
    public static IReadOnlySet<string> GetAllStringValues() => _allPermissions;
}
