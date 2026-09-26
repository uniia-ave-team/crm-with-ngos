using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

/// <summary>
/// Represents an asynchronous authorization filter that validates if the current user's roles
/// possess all required system access rights retrieved from the high-performance cache.
/// </summary>
public partial class AccessRightAuthorizationHandler(
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<AccessRightAuthorizationHandler> logger)
    : AuthorizationHandler<AccessRightRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessRightRequirement requirement)
    {
        var user = context.User;

        var roleIds = currentUserService.GetRoleIds();

        if (roleIds.Count == 0)
        {
            LogMissingRoleIdClaim(logger, user.Identity?.Name ?? "Unknown");
            return;
        }

        bool hasAccess = await permissionService.HasAccessAsync(
            roleIds,
            requirement.RequiredRight,
            CancellationToken.None);

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
        else
        {
            LogAccessDenied(logger, user.Identity.Name ?? "Unknown", string.Join(", ", roleIds));
        }
    }

    [LoggerMessage(EventId = LogEventIds.MissingRoleIdClaim, Level = LogLevel.Warning, Message = "Authorization failed: 'RoleId' claims are missing or invalid for user '{UserName}'.")]
    private static partial void LogMissingRoleIdClaim(ILogger logger, string userName);

    [LoggerMessage(EventId = LogEventIds.AccessDenied, Level = LogLevel.Warning, Message = "Access denied for user '{UserName}' lacking required access rights for Role IDs '{RoleIds}'.")]
    private static partial void LogAccessDenied(ILogger logger, string userName, string roleIds);
}
