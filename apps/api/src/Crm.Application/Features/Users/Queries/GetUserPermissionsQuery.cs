using Crm.Application.Common.Interfaces;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Query to fetch all unique UI permissions for a specific user's roles.
/// </summary>
/// <param name="RoleIds">The role identifiers for which to fetch permissions, populated by the request pipeline when omitted.</param>
public record GetUserPermissionsQuery(List<Guid>? RoleIds = null) : IQuery<List<string>>, ICurrentUserRolesAware
{
    /// <summary>
    /// Gets or sets the role identifiers for which to fetch unique UI permissions.
    /// </summary>
    public List<Guid> RoleIds { get; set; } = RoleIds ?? [];

    /// <inheritdoc />
    List<Guid> ICurrentUserRolesAware.CurrentUserRoleIds
    {
        get => RoleIds;
        set => RoleIds = value;
    }
}
