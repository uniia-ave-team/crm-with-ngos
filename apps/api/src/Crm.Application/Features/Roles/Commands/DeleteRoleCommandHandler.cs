using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="DeleteRoleCommand"/> to remove an existing Identity Role.
/// </summary>
public partial class DeleteRoleCommandHandler(
    RoleManager<AuthRole> roleManager,
    UserManager<AuthUser> userManager,
    IRolePermissionsCache rolesCache,
    ILogger<DeleteRoleCommandHandler> logger) : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        LogDeletingRole(logger, request.Id);

        var role = await roleManager.FindByIdAsync(request.Id.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), request.Id);

        if (role.Name == RoleConsts.Admin)
        {
            LogCannotModifySystemRole(logger, role.Name);
            throw new InvalidOperationException($"The system role '{role.Name}' cannot be deleted.");
        }

        var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
        if (usersInRole.Count != 0)
        {
            LogRoleInUse(logger, request.Id, role.Name!, usersInRole.Count);
            throw new InvalidOperationException($"Cannot delete role '{role.Name}' because it has {usersInRole.Count} assigned user(s).");
        }

        var result = await roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleDeletionFailed(logger, request.Id, errors);
            throw new InvalidOperationException($"Role deletion failed: {errors}");
        }

        await rolesCache.RemoveRolePermissionsAsync(request.Id, cancellationToken);

        LogRoleDeletedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingRole, Level = LogLevel.Information, Message = "Initiating deletion for role ID {RoleId}")]
    private static partial void LogDeletingRole(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.DeleteRoleCommandHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Deletion failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.CannotModifySystemRole, Level = LogLevel.Warning, Message = "Attempted to delete system role: {RoleName}")]
    private static partial void LogCannotModifySystemRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleInUse, Level = LogLevel.Warning, Message = "Cannot delete role '{RoleName}' (ID: {RoleId}) because it is currently assigned to {UserCount} user(s).")]
    private static partial void LogRoleInUse(ILogger logger, Guid roleId, string roleName, int userCount);

    [LoggerMessage(EventId = LogEventIds.RoleDeletionFailed, Level = LogLevel.Warning, Message = "Failed to delete role with ID {RoleId}. Reason: {Errors}")]
    private static partial void LogRoleDeletionFailed(ILogger logger, Guid roleId, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleDeletedSuccessfully, Level = LogLevel.Information, Message = "Role with ID {RoleId} successfully deleted.")]
    private static partial void LogRoleDeletedSuccessfully(ILogger logger, Guid roleId);
}
