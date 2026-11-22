using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="RemoveClaimFromRoleCommand"/> to remove a claim from an Identity Role.
/// </summary>
/// <param name="identityService">The service used to interact with the Identity system for role management.</param>
/// <param name="authRoleRepository">The repository used to retrieve and manage role data.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim removal process.</param>
public sealed partial class RemoveClaimFromRoleCommandHandler(
    IRoleIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
    IRolePermissionsCache rolesCache,
    ILogger<RemoveClaimFromRoleCommandHandler> logger) : ICommandHandler<RemoveClaimFromRoleCommand>
{
    public async ValueTask<Unit> Handle(RemoveClaimFromRoleCommand command, CancellationToken cancellationToken)
    {
        LogRemovingClaim(logger, command.ClaimValue, command.RoleId);

        if (!PermissionExtensions.GetAllStringValues().Contains(command.ClaimValue))
        {
            LogInvalidClaimAttempt(logger, command.ClaimValue, command.RoleId);
            throw new DomainValidationException($"The permission '{command.ClaimValue}' does not exist in the system.");
        }

        var role = await authRoleRepository.GetAsync<RoleUserDto>(command.RoleId, cancellationToken);

        if (string.Equals(role.Name, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            LogCannotModifyAdminRole(logger, command.ClaimValue, command.RoleId);
            throw new DomainValidationException("Modification of claims for the Administrator role is strictly prohibited.");
        }

        await identityService.RemovePermissionFromRoleAsync(command.RoleId, command.ClaimValue, cancellationToken);

        LogClaimRemovedSuccessfully(logger, command.ClaimValue, command.RoleId);

        var permissions = await identityService.GetRolePermissionsAsync(role.Id, cancellationToken);

        await rolesCache.SetRolePermissionsAsync(command.RoleId, permissions, cancellationToken);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RemovingClaim, Level = LogLevel.Information, Message = "Initiating removal of claim '{ClaimValue}' from role ID: {RoleId}")]
    private static partial void LogRemovingClaim(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.InvalidClaimToRemoveAttempt, Level = LogLevel.Warning, Message = "Attempted to remove an invalid or non-existent claim '{ClaimValue}' from role ID {RoleId}.")]
    private static partial void LogInvalidClaimAttempt(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleRemovingClaimAttempt, Level = LogLevel.Warning, Message = "Attempted to remove claim '{ClaimValue}' from the Admin role (ID: {RoleId}). Operation rejected.")]
    private static partial void LogCannotModifyAdminRole(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.ClaimRemovedSuccessfully, Level = LogLevel.Information, Message = "Claim '{ClaimValue}' successfully removed from role ID: {RoleId}")]
    private static partial void LogClaimRemovedSuccessfully(ILogger logger, string claimValue, Guid roleId);
}
