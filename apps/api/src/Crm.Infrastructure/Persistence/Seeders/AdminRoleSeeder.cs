using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Crm.Infrastructure.Entities;
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
    IAuthRoleRepository roleRepository,
    IRoleIdentityService roleIdentityService,
    ILogger<AdminRoleSeeder> logger) : IDatabaseSeeder
{
    /// <summary>
    /// Executes the seeding process to orchestrate the creation of the Administrator role and assignment of permissions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous seeding operation.</returns>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        LogCheckingAdminRole(logger, RoleConsts.Admin);

        var adminRole = await EnsureAdminRoleExistsAsync(cancellationToken);

        await EnsureAdminPermissionsAsync(adminRole, cancellationToken);
    }

    /// <summary>
    /// Ensures the default administrative role exists in the identity database.
    /// </summary>
    /// <returns>The existing or newly created <see cref="AuthRole"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the role creation process fails due to validation errors.</exception>
    private async Task<Guid> EnsureAdminRoleExistsAsync(CancellationToken cancellationToken = default)
    {
        Guid roleId;

        try
        {
            roleId = await roleRepository.GetRoleIdByNameAsync(RoleConsts.Admin, cancellationToken);

            LogAdminRoleAlreadyExists(logger, RoleConsts.Admin);
        }
        catch (EntityNotFoundException)
        {
            roleId = await roleIdentityService.CreateRoleAsync(RoleConsts.Admin, cancellationToken: cancellationToken);

            LogAdminRoleCreatedSuccessfully(logger, RoleConsts.Admin, roleId);
        }

        return roleId;
    }

    /// <summary>
    /// Synchronizes the claims of the provided role with the complete set of system permissions.
    /// </summary>
    /// <param name="roleId">The ID of the role to which missing permissions will be assigned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    private async Task EnsureAdminPermissionsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var existingPermissions = await roleIdentityService.GetRolePermissionsAsync(roleId, cancellationToken);

        var missingPermissions = PermissionExtensions.GetAllStringValues()
            .Where(permissionValue => !existingPermissions.Contains(permissionValue))
            .ToList();

        if (missingPermissions.Count == 0)
        {
            LogAllClaimsAlreadyAssigned(logger, RoleConsts.Admin);
            return;
        }

        int addedCount = 0;

        foreach (string permissionValue in missingPermissions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await roleIdentityService.AddPermissionToRoleAsync(roleId, permissionValue, cancellationToken);
        }

        LogClaimsAddedSuccessfully(logger, addedCount, RoleConsts.Admin);
    }

    [LoggerMessage(EventId = LogEventIds.CheckingAdminRole, Level = LogLevel.Information, Message = "Checking if default admin role '{RoleName}' exists.")]
    private static partial void LogCheckingAdminRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.AdminRoleCreatedSuccessfully, Level = LogLevel.Information, Message = "Admin role '{RoleName}' successfully created with ID: {RoleId}")]
    private static partial void LogAdminRoleCreatedSuccessfully(ILogger logger, string roleName, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.AdminRoleAlreadyExists, Level = LogLevel.Information, Message = "Admin role '{RoleName}' already exists in the system.")]
    private static partial void LogAdminRoleAlreadyExists(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.ClaimsAddedSuccessfully, Level = LogLevel.Information, Message = "Successfully added {Count} new permission claim(s) to role '{RoleName}'.")]
    private static partial void LogClaimsAddedSuccessfully(ILogger logger, int count, string roleName);

    [LoggerMessage(EventId = LogEventIds.AllClaimsAlreadyAssigned, Level = LogLevel.Information, Message = "All system permissions are already assigned to role '{RoleName}'.")]
    private static partial void LogAllClaimsAlreadyAssigned(ILogger logger, string roleName);
}
