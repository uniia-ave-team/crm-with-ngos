using System.Security.Claims;
using Crm.Application.Common.Consts;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder responsible for initializing the default Administrator role and synchronizing all available system permissions.
/// </summary>
/// <remarks>
/// This seeder is designed to be idempotent. It safely checks for the existence of the role and its claims
/// before applying any modifications, ensuring no duplicate data is created during repeated application startups.
/// </remarks>
public partial class AdminRoleSeeder(
    RoleManager<AuthRole> roleManager,
    ILogger<AdminRoleSeeder> logger) : IDatabaseSeeder
{
    /// <summary>
    /// Executes the seeding process to orchestrate the creation of the Administrator role and assignment of permissions.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous seeding operation.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        LogCheckingAdminRole(logger, RoleConsts.Admin);

        var adminRole = await EnsureAdminRoleExistsAsync();

        await EnsureAdminPermissionsAsync(adminRole, ct);
    }

    /// <summary>
    /// Ensures the default administrative role exists in the identity database.
    /// </summary>
    /// <returns>The existing or newly created <see cref="AuthRole"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the role creation process fails due to validation errors.</exception>
    private async Task<AuthRole> EnsureAdminRoleExistsAsync()
    {
        var adminRole = await roleManager.FindByNameAsync(RoleConsts.Admin);

        if (adminRole != null)
        {
            LogAdminRoleAlreadyExists(logger, RoleConsts.Admin);
            return adminRole;
        }

        adminRole = new AuthRole { Name = RoleConsts.Admin };
        var createResult = await roleManager.CreateAsync(adminRole);

        if (!createResult.Succeeded)
        {
            string errors = string.Join(" | ", createResult.Errors.Select(e => e.Description));
            LogAdminRoleCreationFailed(logger, RoleConsts.Admin, errors);

            throw new InvalidOperationException($"Failed to seed Admin role: {errors}");
        }

        LogAdminRoleCreatedSuccessfully(logger, RoleConsts.Admin, adminRole.Id);

        return adminRole;
    }

    /// <summary>
    /// Synchronizes the claims of the provided role with the complete set of system permissions.
    /// </summary>
    /// <param name="adminRole">The role to which missing permissions will be assigned.</param>
    /// <param name="ct">The cancellation token.</param>
    private async Task EnsureAdminPermissionsAsync(AuthRole adminRole, CancellationToken ct)
    {
        var existingClaims = await roleManager.GetClaimsAsync(adminRole);

        var missingPermissions = PermissionExtensions.GetAllStringValues()
            .Where(permissionValue => !existingClaims.Any(c =>
                c.Type == PermissionExtensions.ClaimType &&
                c.Value == permissionValue))
            .ToList();

        if (missingPermissions.Count == 0)
        {
            LogAllClaimsAlreadyAssigned(logger, RoleConsts.Admin);
            return;
        }

        int addedCount = 0;

        foreach (string permissionValue in missingPermissions)
        {
            ct.ThrowIfCancellationRequested();

            var claim = new Claim(PermissionExtensions.ClaimType, permissionValue);
            var addClaimResult = await roleManager.AddClaimAsync(adminRole, claim);

            if (addClaimResult.Succeeded)
            {
                addedCount++;
            }
            else
            {
                string errors = string.Join(" | ", addClaimResult.Errors.Select(e => e.Description));
                LogClaimAdditionFailed(logger, permissionValue, RoleConsts.Admin, errors);
            }
        }

        LogClaimsAddedSuccessfully(logger, addedCount, RoleConsts.Admin);
    }

    [LoggerMessage(EventId = LogEventIds.CheckingAdminRole, Level = LogLevel.Information, Message = "Checking if default admin role '{RoleName}' exists.")]
    private static partial void LogCheckingAdminRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.AdminRoleCreationFailed, Level = LogLevel.Error, Message = "Failed to create admin role '{RoleName}'. Reason: {Errors}")]
    private static partial void LogAdminRoleCreationFailed(ILogger logger, string roleName, string errors);

    [LoggerMessage(EventId = LogEventIds.AdminRoleCreatedSuccessfully, Level = LogLevel.Information, Message = "Admin role '{RoleName}' successfully created with ID: {RoleId}")]
    private static partial void LogAdminRoleCreatedSuccessfully(ILogger logger, string roleName, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleAlreadyExists, Level = LogLevel.Information, Message = "Admin role '{RoleName}' already exists in the system.")]
    private static partial void LogAdminRoleAlreadyExists(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.ClaimAdditionFailed, Level = LogLevel.Warning, Message = "Failed to add permission claim '{ClaimValue}' to role '{RoleName}'. Reason: {Errors}")]
    private static partial void LogClaimAdditionFailed(ILogger logger, string claimValue, string roleName, string errors);

    [LoggerMessage(EventId = LogEventIds.ClaimsAddedSuccessfully, Level = LogLevel.Information, Message = "Successfully added {Count} new permission claim(s) to role '{RoleName}'.")]
    private static partial void LogClaimsAddedSuccessfully(ILogger logger, int count, string roleName);

    [LoggerMessage(EventId = LogEventIds.AllClaimsAlreadyAssigned, Level = LogLevel.Information, Message = "All system permissions are already assigned to role '{RoleName}'.")]
    private static partial void LogAllClaimsAlreadyAssigned(ILogger logger, string roleName);
}
