using Crm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

/// <summary>
/// Authorization requirement representing a specific <see cref="AccessRight"/> needed to access an endpoint.
/// </summary>
/// <param name="requiredRight">The access right required by the policy.</param>
public sealed class AccessRightRequirement(AccessRight requiredRight) : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the required <see cref="AccessRight"/>.
    /// </summary>
    public AccessRight RequiredRight { get; } = requiredRight;
}
