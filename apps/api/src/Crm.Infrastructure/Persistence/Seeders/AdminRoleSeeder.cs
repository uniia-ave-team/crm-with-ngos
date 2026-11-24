using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder responsible for initializing the default Administrator role and synchronizing all available system permissions.
/// </summary>
/// <remarks>
/// This seeder is designed to be idempotent. It safely checks for the existence of the role and its claims
/// before applying any modifications, ensuring no duplicate data is created during repeated application startups.
/// </remarks>
/// <param name="roleRepository">The role repository used to look up the default Administrator role.</param>
/// <param name="roleIdentityService">The role identity service used to create roles and synchronize permission claims.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class AdminRoleSeeder(
    IAuthRoleRepository roleRepository,
    IRoleIdentityService roleIdentityService,
    ILogger<AdminRoleSeeder> logger) : IDatabaseSeeder
{
    /// <inheritdoc />
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        LogCheckingAdminRole(logger, RoleConstants.Admin);

        Guid adminRoleId = await EnsureAdminRoleExistsAsync(cancellationToken);

        await EnsureAdminPermissionsAsync(adminRoleId, cancellationToken);
    }

    private async Task<Guid> EnsureAdminRoleExistsAsync(CancellationToken cancellationToken)
    {
        Guid? existingRoleId = await roleRepository.FindRoleIdByNameAsync(RoleConstants.Admin, cancellationToken);
        if (existingRoleId is { } roleId)
        {
            LogAdminRoleAlreadyExists(logger, RoleConstants.Admin);
            return roleId;
        }

        Guid createdRoleId = await roleIdentityService.CreateRoleAsync(RoleConstants.Admin, cancellationToken: cancellationToken);
        LogAdminRoleCreatedSuccessfully(logger, RoleConstants.Admin, createdRoleId);
        return createdRoleId;
    }

    private async Task EnsureAdminPermissionsAsync(Guid roleId, CancellationToken cancellationToken)
    {
        int addedCount = await roleIdentityService.AddPermissionsToRoleAsync(
            roleId,
            PermissionExtensions.GetAllStringValues(),
            cancellationToken);

        if (addedCount == 0)
        {
            LogAllClaimsAlreadyAssigned(logger, RoleConstants.Admin);
            return;
        }

        LogClaimsAddedSuccessfully(logger, addedCount, RoleConstants.Admin);
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
