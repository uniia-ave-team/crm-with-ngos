using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="UpdateRoleCommand"/> to modify an existing Identity Role.
/// </summary>
/// <param name="authRoleRepository">The repository used to retrieve role data.</param>
/// <param name="roleIdentityService">The service used to interact with the Identity system for role updates.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role update process.</param>
public sealed partial class UpdateRoleCommandHandler(
    IAuthRoleRepository authRoleRepository,
    IRoleIdentityService roleIdentityService,
    ILogger<UpdateRoleCommandHandler> logger) : ICommandHandler<UpdateRoleCommand>
{
    public async ValueTask<Unit> Handle(UpdateRoleCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingRole(logger, command.Id, command.NewName);

        var role = await authRoleRepository.GetAsync<RoleUserDto>(command.Id, cancellationToken);

        if (string.Equals(role.Name, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase))
        {
            LogCannotModifySystemRole(logger, role.Name);
            throw new DomainValidationException($"The system role '{role.Name}' cannot be modified.");
        }

        await roleIdentityService.UpdateRoleAsync(role.Id, command.NewName, command.FeminitiveName, command.PluralName, cancellationToken);

        LogRoleUpdatedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingRole, Level = LogLevel.Information, Message = "Initiating update for role ID {RoleId} to new name: {NewName}")]
    private static partial void LogUpdatingRole(ILogger logger, Guid roleId, string newName);

    [LoggerMessage(EventId = LogEventIds.UpdateRoleCommandHandlerCannotModifySystemRole, Level = LogLevel.Warning, Message = "Attempted to modify system role: {RoleName}")]
    private static partial void LogCannotModifySystemRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleUpdatedSuccessfully, Level = LogLevel.Information, Message = "Role with ID {RoleId} successfully updated.")]
    private static partial void LogRoleUpdatedSuccessfully(ILogger logger, Guid roleId);
}
