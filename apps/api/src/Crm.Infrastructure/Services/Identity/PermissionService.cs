using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Implements <see cref="IPermissionService"/> to evaluate access rights
/// by aggregating permissions from one or more roles using <see cref="IRolePermissionsCache"/>.
/// </summary>
/// <param name="permissionsCache">The cache service used to retrieve permissions for specific roles.</param>
public sealed class PermissionService(IRolePermissionsCache permissionsCache) : IPermissionService
{
    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(List<Guid> roleIds, AccessRight requiredRight, CancellationToken cancellationToken = default)
    {
        if (roleIds is null || roleIds.Count == 0)
        {
            return false;
        }

        string requiredClaim = requiredRight.ToClaimValue();

        for (int i = 0; i < roleIds.Count; i++)
        {
            var permissions = await permissionsCache.GetRolePermissionsAsync(roleIds[i], cancellationToken);
            if (permissions.Contains(requiredClaim))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> HasAccessAsync(List<Guid> roleIds, IEnumerable<AccessRight> requiredRights, CancellationToken cancellationToken = default)
    {
        if (roleIds is null || roleIds.Count == 0)
        {
            return false;
        }

        if (roleIds.Count == 1)
        {
            var permissions = await permissionsCache.GetRolePermissionsAsync(roleIds[0], cancellationToken);
            return requiredRights.All(right => permissions.Contains(right.ToClaimValue()));
        }

        var tasks = roleIds.Select(id => permissionsCache.GetRolePermissionsAsync(id, cancellationToken));
        var rolePermissionsCollection = await Task.WhenAll(tasks);

        foreach (var right in requiredRights)
        {
            string requiredClaim = right.ToClaimValue();
            bool granted = false;

            for (int i = 0; i < rolePermissionsCollection.Length; i++)
            {
                if (rolePermissionsCollection[i].Contains(requiredClaim))
                {
                    granted = true;
                    break;
                }
            }

            if (!granted)
            {
                return false;
            }
        }

        return true;
    }
}
