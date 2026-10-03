using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Roles.Commands;

/// <summary>
/// Handles the <see cref="CreateRoleCommand"/> to create a new Identity Role in the system.
/// </summary>
/// <param name="identityService">The service used to interact with the Identity system for role management.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role creation process.</param>
public partial class CreateRoleCommandHandler(
    IRoleIdentityService identityService,
    ILogger<CreateRoleCommandHandler> logger) : IRequestHandler<CreateRoleCommand, Guid>
{
    public async Task<Guid> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        LogCreatingRole(logger, request.Name);

        var roleId = await identityService.CreateRoleAsync(request.Name, request.FeminitiveName, request.PluralName, cancellationToken);

        LogRoleCreatedSuccessfully(logger, request.Name, roleId);

        return roleId;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingRole, Level = LogLevel.Information, Message = "Initiating creation of role: {RoleName}")]
    private static partial void LogCreatingRole(ILogger logger, string roleName);

    [LoggerMessage(EventId = LogEventIds.RoleCreationFailed, Level = LogLevel.Warning, Message = "Failed to create role '{RoleName}'. Reason: {Errors}")]
    private static partial void LogRoleCreationFailed(ILogger logger, string roleName, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleCreatedSuccessfully, Level = LogLevel.Information, Message = "Role '{RoleName}' successfully created with ID: {RoleId}")]
    private static partial void LogRoleCreatedSuccessfully(ILogger logger, string roleName, Guid roleId);
}
