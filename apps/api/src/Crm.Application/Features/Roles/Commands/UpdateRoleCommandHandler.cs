using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="UpdateRoleCommand"/> to modify an existing Identity Role.
/// </summary>
public partial class UpdateRoleCommandHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService roleIdentityService,
    ILogger<UpdateRoleCommandHandler> logger) : IRequestHandler<UpdateRoleCommand>
{
    public async Task Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingRole(logger, request.Id, request.NewName);

        var role = await authRoleRepository.GetAsync<RoleUserDto>(request.Id, cancellationToken);

        if (role.Name == RoleConsts.Admin)
        {
            LogCannotModifySystemRole(logger, role.Name);
            throw new InvalidOperationException($"The system role '{role.Name}' cannot be modified.");
        }

        await roleIdentityService.UpdateRoleAsync(role.Id, request.NewName, request.FeminitiveName, request.PluralName, cancellationToken);

        LogRoleUpdatedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingRole, Level = LogLevel.Information, Message = "Initiating update for role ID {RoleId} to new name: {NewName}")]
    private static partial void LogUpdatingRole(ILogger logger, Guid roleId, string newName);

    [LoggerMessage(EventId = LogEventIds.UpdateRoleCommandHandlerCannotModifySystemRole, Level = LogLevel.Warning, Message = "Attempted to modify system role: {RoleName}")]
    private static partial void LogCannotModifySystemRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleUpdatedSuccessfully, Level = LogLevel.Information, Message = "Role with ID {RoleId} successfully updated.")]
    private static partial void LogRoleUpdatedSuccessfully(ILogger logger, Guid roleId);
}
