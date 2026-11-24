using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="DeleteRoleCommand"/> to remove an existing Identity Role.
/// </summary>
/// <param name="roleRepository">The repository used to retrieve role data.</param>
/// <param name="userRepository">The repository used to check for users assigned to the role.</param>
/// <param name="rolesCache">The cache service used to evict permissions for the deleted role.</param>
/// <param name="identityService">The service used to interact with the Identity system for role deletion.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role deletion process.</param>
public sealed partial class DeleteRoleCommandHandler(
    IAuthRoleRepository roleRepository,
    IAuthUserRepository userRepository,
    IRolePermissionsCache rolesCache,
    IRoleIdentityService identityService,
    ILogger<DeleteRoleCommandHandler> logger) : ICommandHandler<DeleteRoleCommand>
{
    public async ValueTask<Unit> Handle(DeleteRoleCommand command, CancellationToken cancellationToken)
    {
        LogDeletingRole(logger, command.Id);

        var role = await roleRepository.GetAsync<RoleUserDto>(command.Id, cancellationToken);

        if (string.Equals(role.Name, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            LogCannotModifySystemRole(logger, role.Name);
            throw new DomainValidationException($"The system role '{role.Name}' cannot be deleted.");
        }

        var usersCount = await userRepository.GetCountByRoleIdAsync(role.Id, cancellationToken);

        if (usersCount != 0)
        {
            LogRoleInUse(logger, command.Id, role.Name, usersCount);
            throw new ConflictException(
                $"Cannot delete role '{role.Name}' because it has {usersCount} assigned user(s).",
                ConflictKind.ReferenceViolation);
        }

        await identityService.DeleteRoleAsync(role.Id, cancellationToken);

        await rolesCache.RemoveRolePermissionsAsync(command.Id, cancellationToken);

        LogRoleDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingRole, Level = LogLevel.Information, Message = "Initiating deletion for role ID {RoleId}")]
    private static partial void LogDeletingRole(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.CannotModifySystemRole, Level = LogLevel.Warning, Message = "Attempted to delete system role: {RoleName}")]
    private static partial void LogCannotModifySystemRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleInUse, Level = LogLevel.Warning, Message = "Cannot delete role '{RoleName}' (ID: {RoleId}) because it is currently assigned to {UserCount} user(s).")]
    private static partial void LogRoleInUse(ILogger logger, Guid roleId, string roleName, int userCount);

    [LoggerMessage(EventId = LogEventIds.RoleDeletedSuccessfully, Level = LogLevel.Information, Message = "Role with ID {RoleId} successfully deleted.")]
    private static partial void LogRoleDeletedSuccessfully(ILogger logger, Guid roleId);
}
