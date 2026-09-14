using System.Data;
using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="RemoveClaimFromRoleCommand"/> to remove a claim from an Identity Role.
/// </summary>
/// <param name="roleManager">The ASP.NET Core Identity role manager used for role retrieval and claim management.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim removal process.</param>
public partial class RemoveClaimFromRoleCommandHandler(
    RoleManager<AuthRole> roleManager,
    IRolePermissionsCache rolesCache,
    ILogger<RemoveClaimFromRoleCommandHandler> logger) : IRequestHandler<RemoveClaimFromRoleCommand>
{
    public async Task Handle(RemoveClaimFromRoleCommand request, CancellationToken cancellationToken)
    {
        LogRemovingClaim(logger, request.ClaimValue, request.RoleId);

        if (!PermissionExtensions.GetAllStringValues().Contains(request.ClaimValue))
        {
            LogInvalidClaimAttempt(logger, request.ClaimValue, request.RoleId);
            throw new ArgumentException($"The permission '{request.ClaimValue}' does not exist in the system.");
        }

        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());

        if (role == null)
        {
            LogRoleNotFound(logger, request.RoleId);
            throw new EntityNotFoundException(nameof(AuthRole), request.RoleId);
        }

        if (string.Equals(role.Name, RoleConsts.Admin, StringComparison.OrdinalIgnoreCase))
        {
            LogCannotModifyAdminRole(logger, request.ClaimValue, request.RoleId);
            throw new InvalidOperationException("Modification of claims for the Administrator role is strictly prohibited.");
        }

        var claim = new Claim(PermissionExtensions.ClaimType, request.ClaimValue);

        var result = await roleManager.RemoveClaimAsync(role, claim);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogClaimRemovalFailed(logger, request.ClaimValue, request.RoleId, errors);

            throw new InvalidOperationException($"Failed to remove claim from role: {errors}");
        }

        LogClaimRemovedSuccessfully(logger, request.ClaimValue, request.RoleId);

        var claims = await roleManager.GetClaimsAsync(role);
        var permissions = claims
            .Where(c => c.Type == PermissionExtensions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        await rolesCache.SetRolePermissionsAsync(request.RoleId, permissions, cancellationToken);
    }

    [LoggerMessage(EventId = LogEventIds.RemovingClaim, Level = LogLevel.Information, Message = "Initiating removal of claim '{ClaimValue}' from role ID: {RoleId}")]
    private static partial void LogRemovingClaim(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.InvalidClaimToRemoveAttempt, Level = LogLevel.Warning, Message = "Attempted to remove an invalid or non-existent claim '{ClaimValue}' from role ID {RoleId}.")]
    private static partial void LogInvalidClaimAttempt(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RemoveClaimFromRoleCommandHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Claim removal failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleRemovingClaimAttempt, Level = LogLevel.Warning, Message = "Attempted to remove claim '{ClaimValue}' from the Admin role (ID: {RoleId}). Operation rejected.")]
    private static partial void LogCannotModifyAdminRole(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.ClaimRemovalFailed, Level = LogLevel.Warning, Message = "Failed to remove claim '{ClaimValue}' from role ID {RoleId}. Reason: {Errors}")]
    private static partial void LogClaimRemovalFailed(ILogger logger, string claimValue, Guid roleId, string errors);

    [LoggerMessage(EventId = LogEventIds.ClaimRemovedSuccessfully, Level = LogLevel.Information, Message = "Claim '{ClaimValue}' successfully removed from role ID: {RoleId}")]
    private static partial void LogClaimRemovedSuccessfully(ILogger logger, string claimValue, Guid roleId);
}
