using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Implements the <see cref="IPermissionService"/> to evaluate access rights
/// by aggregating permissions from multiple roles using the high-performance role permissions cache.
/// </summary>
/// <param name="permissionsCache">The cache service used to retrieve permissions for specific roles.</param>
public class PermissionService(IRolePermissionsCache permissionsCache) : IPermissionService
{
    /// <inheritdoc />
    public Task<bool> HasAccessAsync(List<Guid> roleIds, AccessRight requiredRight, CancellationToken cancellationToken = default)
    {
        return HasAccessAsync(roleIds, [requiredRight], cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(List<Guid> roleIds, IEnumerable<AccessRight> requiredRights, CancellationToken cancellationToken = default)
    {
        if (roleIds is null || roleIds.Count == 0)
        {
            return false;
        }

        var tasks = roleIds.Select(id => permissionsCache.GetRolePermissionsAsync(id, cancellationToken));
        var rolePermissionsCollection = await Task.WhenAll(tasks);

        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rolePermissions in rolePermissionsCollection)
        {
            allPermissions.UnionWith(rolePermissions);
        }

        foreach (var right in requiredRights)
        {
            string requiredRightString = right.ToClaimValue();

            if (!allPermissions.Contains(requiredRightString))
            {
                return false;
            }
        }

        return true;
    }
}
