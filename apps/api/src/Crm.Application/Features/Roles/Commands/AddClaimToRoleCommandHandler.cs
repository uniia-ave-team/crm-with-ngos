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
/// Handles the <see cref="AddClaimToRoleCommand"/> to assign a new claim to an Identity Role.
/// </summary>
/// <param name="authRoleRepository">The repository used to retrieve and manage role data.</param>
/// <param name="identityService">The service used to interact with the Identity system for role and claim management.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim assignment process.</param>
public partial class AddClaimToRoleCommandHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService identityService,
    IRolePermissionsCache rolesCache,
    ILogger<AddClaimToRoleCommandHandler> logger) : IRequestHandler<AddClaimToRoleCommand>
{
    public async Task Handle(AddClaimToRoleCommand request, CancellationToken cancellationToken)
    {
        LogAddingClaim(logger, request.ClaimValue, request.RoleId);

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

        await identityService.AddPermissionToRoleAsync(request.RoleId, request.ClaimValue, cancellationToken);

        LogClaimAddedSuccessfully(logger, request.ClaimValue, request.RoleId);

        var permissions = await identityService.GetRolePermissionsAsync(request.RoleId, cancellationToken);

        await rolesCache.SetRolePermissionsAsync(request.RoleId, permissions, cancellationToken);
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
