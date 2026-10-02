using Crm.Domain.Enums;

namespace Crm.Application.Interfaces;

/// <summary>
/// Provides centralized logic for evaluating user access rights based on their claims and cached role permissions.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if the user possesses a specific access right across any of their assigned roles.
    /// </summary>
    Task<bool> HasAccessAsync(List<Guid> roleIds, AccessRight requiredRight, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the user possesses ALL specified access rights.
    /// </summary>
    Task<bool> HasAccessAsync(List<Guid> roleIds, IEnumerable<AccessRight> requiredRights, CancellationToken cancellationToken = default);
}
