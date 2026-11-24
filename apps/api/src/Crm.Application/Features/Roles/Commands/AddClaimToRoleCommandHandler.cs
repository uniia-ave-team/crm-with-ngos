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
/// Handles the <see cref="AddClaimToRoleCommand"/> to assign a new claim to an Identity Role.
/// </summary>
/// <param name="authRoleRepository">The repository used to retrieve and manage role data.</param>
/// <param name="identityService">The service used to interact with the Identity system for role and claim management.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim assignment process.</param>
public sealed partial class AddClaimToRoleCommandHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService identityService,
    IRolePermissionsCache rolesCache,
    ILogger<AddClaimToRoleCommandHandler> logger) : ICommandHandler<AddClaimToRoleCommand>
{
    public async ValueTask<Unit> Handle(AddClaimToRoleCommand command, CancellationToken cancellationToken)
    {
        LogAddingClaim(logger, command.ClaimValue, command.RoleId);

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

        await identityService.AddPermissionToRoleAsync(command.RoleId, command.ClaimValue, cancellationToken);

        LogClaimAddedSuccessfully(logger, command.ClaimValue, command.RoleId);

        var permissions = await identityService.GetRolePermissionsAsync(command.RoleId, cancellationToken);

        await rolesCache.SetRolePermissionsAsync(command.RoleId, permissions, cancellationToken);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AddingClaim, Level = LogLevel.Information, Message = "Initiating addition of claim '{ClaimValue}' to role ID: {RoleId}")]
    private static partial void LogAddingClaim(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.InvalidClaimToAddAttempt, Level = LogLevel.Warning, Message = "Attempted to assign an invalid or non-existent claim '{ClaimValue}' to role ID {RoleId}.")]
    private static partial void LogInvalidClaimAttempt(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleAddingClaimAttempt, Level = LogLevel.Warning, Message = "Attempted to add claim '{ClaimValue}' to the Admin role (ID: {RoleId}). Operation rejected.")]
    private static partial void LogCannotModifyAdminRole(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.ClaimAddedSuccessfully, Level = LogLevel.Information, Message = "Claim '{ClaimValue}' successfully added to role ID: {RoleId}")]
    private static partial void LogClaimAddedSuccessfully(ILogger logger, string claimValue, Guid roleId);
}
