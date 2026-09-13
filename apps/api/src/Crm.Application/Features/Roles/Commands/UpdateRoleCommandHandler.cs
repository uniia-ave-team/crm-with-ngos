using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Commands;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="UpdateRoleCommand"/> to modify an existing Identity Role.
/// </summary>
public partial class UpdateRoleCommandHandler(
    RoleManager<AuthRole> roleManager,
    ILogger<UpdateRoleCommandHandler> logger) : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingRole(logger, request.Id, request.NewName);

        var role = await roleManager.FindByIdAsync(request.Id.ToString())
            ?? throw new EntityNotFoundException(nameof(AuthRole), request.Id);

        if (role.Name == RoleConsts.Admin)
        {
            LogCannotModifySystemRole(logger, role.Name);
            throw new InvalidOperationException($"The system role '{role.Name}' cannot be modified.");
        }

        role.Name = request.NewName;

        var result = await roleManager.UpdateAsync(role);
        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleUpdateFailed(logger, request.Id, errors);
            throw new InvalidOperationException($"Role update failed: {errors}");
        }

        LogRoleUpdatedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingRole, Level = LogLevel.Information, Message = "Initiating update for role ID {RoleId} to new name: {NewName}")]
    private static partial void LogUpdatingRole(ILogger logger, Guid roleId, string newName);

    [LoggerMessage(EventId = LogEventIds.UpdateRoleCommandHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Update failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.UpdateRoleCommandHandlerCannotModifySystemRole, Level = LogLevel.Warning, Message = "Attempted to modify system role: {RoleName}")]
    private static partial void LogCannotModifySystemRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleUpdateFailed, Level = LogLevel.Warning, Message = "Failed to update role with ID {RoleId}. Reason: {Errors}")]
    private static partial void LogRoleUpdateFailed(ILogger logger, Guid roleId, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleUpdatedSuccessfully, Level = LogLevel.Information, Message = "Role with ID {RoleId} successfully updated.")]
    private static partial void LogRoleUpdatedSuccessfully(ILogger logger, Guid roleId);
}
