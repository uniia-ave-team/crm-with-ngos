using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="CreateRoleCommand"/> to create a new Identity Role in the system.
/// </summary>
/// <param name="identityService">The service used to interact with the Identity system for role management.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role creation process.</param>
public sealed partial class CreateRoleCommandHandler(
    IRoleIdentityService identityService,
    ILogger<CreateRoleCommandHandler> logger) : ICommandHandler<CreateRoleCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateRoleCommand command, CancellationToken cancellationToken)
    {
        LogCreatingRole(logger, command.Name);

        var roleId = await identityService.CreateRoleAsync(command.Name, command.FeminitiveName, command.PluralName, cancellationToken);

        LogRoleCreatedSuccessfully(logger, command.Name, roleId);

        return roleId;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingRole, Level = LogLevel.Information, Message = "Initiating creation of role: {RoleName}")]
    private static partial void LogCreatingRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleCreatedSuccessfully, Level = LogLevel.Information, Message = "Role '{RoleName}' successfully created with ID: {RoleId}")]
    private static partial void LogRoleCreatedSuccessfully(ILogger logger, string roleName, Guid roleId);
}
