using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Security;

/// <summary>
/// Represents an asynchronous authorization filter that validates if the current user's roles
/// possess all required system access rights retrieved from the high-performance cache.
/// </summary>
public partial class AccessRightAuthorizationFilter(
    IRolePermissionsCache permissionsCache,
    ILogger<AccessRightAuthorizationFilter> logger) : IAsyncAuthorizationFilter
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

        var roleIdClaims = user.FindAll(CustomClaimTypes.RoleId).ToList();
        if (roleIdClaims.Count == 0)
        {
            LogMissingRoleIdClaim(logger, user.Identity.Name ?? "Unknown");
            context.Result = new ForbidResult();
            return;
        }

        var roleIds = new List<Guid>();
        foreach (var claim in roleIdClaims)
        {
            if (Guid.TryParse(claim.Value, out var roleId))
            {
                roleIds.Add(roleId);
            }
        }

        if (roleIds.Count == 0)
        {
            LogMissingRoleIdClaim(logger, user.Identity.Name ?? "Unknown");
            context.Result = new ForbidResult();
            return;
        }

        bool hasAccess = await HasRequiredRightsAsync(roleIds, attributes, context.HttpContext.RequestAborted);

        if (!hasAccess)
        {
            LogAccessDenied(logger, user.Identity.Name ?? "Unknown", string.Join(", ", roleIds));
            context.Result = new ForbidResult();
        }
    }

    /// <summary>
    /// Helper method to verify if the combined permissions from all roles have all specified access rights.
    /// </summary>
    private async Task<bool> HasRequiredRightsAsync(IEnumerable<Guid> roleIds, List<HasAccessRightAttribute> attributes, CancellationToken cancellationToken)
    {
        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleId in roleIds)
        {
            var rolePermissions = await permissionsCache.GetRolePermissionsAsync(roleId, cancellationToken);
            foreach (var permission in rolePermissions)
            {
                allPermissions.Add(permission);
            }
        }

        foreach (var attribute in attributes)
        {
            string requiredRightString = attribute.RequiredRight.ToClaimValue();

            if (!allPermissions.Contains(requiredRightString))
            {
                return false;
            }
        }

        return true;
    }

    [LoggerMessage(EventId = LogEventIds.MissingRoleIdClaim, Level = LogLevel.Warning, Message = "Authorization failed: 'RoleId' claims are missing or invalid for user '{UserName}'.")]
    private static partial void LogMissingRoleIdClaim(ILogger logger, string userName);

    [LoggerMessage(EventId = LogEventIds.AccessDenied, Level = LogLevel.Warning, Message = "Access denied for user '{UserName}' lacking required access rights for Role IDs '{RoleIds}'.")]
    private static partial void LogAccessDenied(ILogger logger, string userName, string roleIds);
}
