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
/// Handles the <see cref="AddClaimToRoleCommand"/> to assign a new claim to an Identity Role.
/// </summary>
/// <param name="roleManager">The ASP.NET Core Identity role manager used for role retrieval and claim assignment.</param>
/// <param name="rolesCache">The cache service used to maintain up-to-date role permissions in memory.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the claim assignment process.</param>
public partial class AddClaimToRoleCommandHandler(
    RoleManager<AuthRole> roleManager,
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

        var result = await roleManager.AddClaimAsync(role, claim);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogClaimAdditionFailed(logger, request.ClaimValue, request.RoleId, errors);

            throw new InvalidOperationException($"Failed to add claim to role: {errors}");
        }

        LogClaimAddedSuccessfully(logger, request.ClaimValue, request.RoleId);

        var claims = await roleManager.GetClaimsAsync(role);
        var permissions = claims
            .Where(c => c.Type == PermissionExtensions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        await rolesCache.SetRolePermissionsAsync(request.RoleId, permissions, cancellationToken);
    }

    [LoggerMessage(EventId = LogEventIds.AddingClaim, Level = LogLevel.Information, Message = "Initiating addition of claim '{ClaimValue}' to role ID: {RoleId}")]
    private static partial void LogAddingClaim(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.InvalidClaimToAddAttempt, Level = LogLevel.Warning, Message = "Attempted to assign an invalid or non-existent claim '{ClaimValue}' to role ID {RoleId}.")]
    private static partial void LogInvalidClaimAttempt(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleNotFound, Level = LogLevel.Warning, Message = "Claim addition failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleAddingClaimAttempt, Level = LogLevel.Warning, Message = "Attempted to add claim '{ClaimValue}' to the Admin role (ID: {RoleId}). Operation rejected.")]
    private static partial void LogCannotModifyAdminRole(ILogger logger, string claimValue, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AddClaimToRoleCommandHandlerClaimAdditionFailed, Level = LogLevel.Warning, Message = "Failed to add claim '{ClaimValue}' to role ID {RoleId}. Reason: {Errors}")]
    private static partial void LogClaimAdditionFailed(ILogger logger, string claimValue, Guid roleId, string errors);

    [LoggerMessage(EventId = LogEventIds.ClaimAddedSuccessfully, Level = LogLevel.Information, Message = "Claim '{ClaimValue}' successfully added to role ID: {RoleId}")]
    private static partial void LogClaimAddedSuccessfully(ILogger logger, string claimValue, Guid roleId);
}
