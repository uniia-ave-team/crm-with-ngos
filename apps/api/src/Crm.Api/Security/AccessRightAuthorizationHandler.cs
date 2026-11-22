using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Crm.Api.Security;

/// <summary>
/// Authorization handler that verifies whether the current user's roles possess
/// the required system access right from the permission cache.
/// </summary>
/// <param name="currentUserService">The service for resolving the current user's role identifiers.</param>
/// <param name="permissionService">The service for evaluating role permissions.</param>
/// <param name="logger">The logger instance for recording authorization failures.</param>
public sealed partial class AccessRightAuthorizationHandler(
    ICurrentUserService currentUserService,
    IPermissionService permissionService,
    ILogger<AccessRightAuthorizationHandler> logger)
    : AuthorizationHandler<AccessRightRequirement>
{
    private const string UnknownUser = "Unknown";

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessRightRequirement requirement)
    {
        string userIdentifier = context.User.FindFirstValue(ClaimTypes.Email)
            ?? context.User.FindFirstValue(CustomClaimTypes.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.Identity?.Name
            ?? UnknownUser;
        var roleIds = currentUserService.GetRoleIds();

        if (roleIds.Count == 0)
        {
            LogMissingRoleIdClaim(logger, userIdentifier);
            return;
        }

        CancellationToken cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted
            : CancellationToken.None;

        bool hasAccess = await permissionService.HasAccessAsync(
            roleIds,
            requirement.RequiredRight,
            cancellationToken);

        if (hasAccess)
        {
            context.Succeed(requirement);
        }
        else
        {
            LogAccessDenied(logger, userIdentifier, string.Join(", ", roleIds));
        }
    }

    [LoggerMessage(EventId = LogEventIds.MissingRoleIdClaim, Level = LogLevel.Warning, Message = "Authorization failed: 'RoleId' claims are missing or invalid for user '{UserName}'.")]
    private static partial void LogMissingRoleIdClaim(ILogger logger, string userName);

    [LoggerMessage(EventId = LogEventIds.AccessDenied, Level = LogLevel.Warning, Message = "Access denied for user '{UserName}' lacking required access rights for Role IDs '{RoleIds}'.")]
    private static partial void LogAccessDenied(ILogger logger, string userName, string roleIds);
}
