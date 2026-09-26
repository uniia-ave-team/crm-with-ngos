using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="RemoveClaimFromRoleCommand"/> to remove a claim from an Identity Role.
/// </summary>
/// <param name="identityService">The service used to interact with the Identity system for role management.</param>
/// <param name="authRoleRepository">The repository used to retrieve and manage role data.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim removal process.</param>
public partial class RemoveClaimFromRoleCommandHandler(
    IRoleIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
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

        var role = await authRoleRepository.GetAsync<RoleUserDto>(request.RoleId, cancellationToken);

        if (string.Equals(role.Name, RoleConsts.Admin, StringComparison.OrdinalIgnoreCase))
        {
            LogCannotModifyAdminRole(logger, request.ClaimValue, request.RoleId);
            throw new InvalidOperationException("Modification of claims for the Administrator role is strictly prohibited.");
        }

        await identityService.RemovePermissionFromRoleAsync(request.RoleId, request.ClaimValue, cancellationToken);

        LogClaimRemovedSuccessfully(logger, request.ClaimValue, request.RoleId);

        var permissions = await identityService.GetRolePermissionsAsync(role.Id, cancellationToken);

        await rolesCache.SetRolePermissionsAsync(request.RoleId, permissions, cancellationToken);
    }

    [LoggerMessage(EventId = LogEventIds.RemovingClaim, Level = LogLevel.Information, Message = "Initiating removal of claim '{ClaimValue}' from role ID: {RoleId}")]
    private static partial void LogRemovingClaim(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.InvalidClaimToRemoveAttempt, Level = LogLevel.Warning, Message = "Attempted to remove an invalid or non-existent claim '{ClaimValue}' from role ID {RoleId}.")]
    private static partial void LogInvalidClaimAttempt(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleRemovingClaimAttempt, Level = LogLevel.Warning, Message = "Attempted to remove claim '{ClaimValue}' from the Admin role (ID: {RoleId}). Operation rejected.")]
    private static partial void LogCannotModifyAdminRole(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.ClaimRemovalFailed, Level = LogLevel.Warning, Message = "Failed to remove claim '{ClaimValue}' from role ID {RoleId}. Reason: {Errors}")]
    private static partial void LogClaimRemovalFailed(ILogger logger, string claimValue, Guid roleId, string errors);

    [LoggerMessage(EventId = LogEventIds.ClaimRemovedSuccessfully, Level = LogLevel.Information, Message = "Claim '{ClaimValue}' successfully removed from role ID: {RoleId}")]
    private static partial void LogClaimRemovedSuccessfully(ILogger logger, string claimValue, Guid roleId);
}
