using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Security;

/// <summary>
/// Represents an asynchronous authorization filter that validates if the current user's roles
/// possess all required system access rights retrieved from the high-performance cache.
/// </summary>
public partial class AccessRightAuthorizationFilter(ILogger<AccessRightAuthorizationFilter> logger) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var attributes = context.ActionDescriptor.EndpointMetadata
            .OfType<HasAccessRightAttribute>()
            .ToList();

        if (attributes.Count == 0)
        {
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var currentUserService = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

        var roleIds = currentUserService.GetRoleIds();

        if (roleIds.Count == 0)
        {
            LogMissingRoleIdClaim(logger, user.Identity.Name ?? "Unknown");
            context.Result = new ForbidResult();
            return;
        }

        var permissionService = context.HttpContext.RequestServices.GetRequiredService<IPermissionService>();

        bool hasAccess = await permissionService.HasAccessAsync(roleIds, attributes.Select(a => a.RequiredRight), context.HttpContext.RequestAborted);

        if (!hasAccess)
        {
            LogAccessDenied(logger, user.Identity.Name ?? "Unknown", string.Join(", ", roleIds));
            context.Result = new ForbidResult();
        }
    }

    [LoggerMessage(EventId = LogEventIds.MissingRoleIdClaim, Level = LogLevel.Warning, Message = "Authorization failed: 'RoleId' claims are missing or invalid for user '{UserName}'.")]
    private static partial void LogMissingRoleIdClaim(ILogger logger, string userName);

    [LoggerMessage(EventId = LogEventIds.AccessDenied, Level = LogLevel.Warning, Message = "Access denied for user '{UserName}' lacking required access rights for Role IDs '{RoleIds}'.")]
    private static partial void LogAccessDenied(ILogger logger, string userName, string roleIds);
}
