using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AssignUserRoleCommand"/> to assign a specific Identity Role to a user.
/// </summary>
/// <param name="roleRepository">The repository used to retrieve role details, such as the role name required for assignment.</param>
/// <param name="identityService">The service used to manage user identities and execute the role assignment.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role assignment process.</param>
public partial class AssignUserRoleCommandHandler(
    IAuthRoleRepository roleRepository,
    IIdentityService identityService,
    ILogger<AssignUserRoleCommandHandler> logger) : IRequestHandler<AssignUserRoleCommand>
{
    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        LogAssigningRole(logger, request.RoleId, request.UserId);

        var role = await roleRepository.GetAsync<RoleUserDto>(request.RoleId, cancellationToken);

        await identityService.AddToRoleAsync(request.UserId, role.Name, cancellationToken);

        LogRoleAssignedSuccessfully(logger, request.RoleId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.AssigningRole, Level = LogLevel.Information, Message = "Initiating assignment of role ID '{RoleId}' to user ID: {UserId}")]
    private static partial void LogAssigningRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RoleAssignedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully assigned to user ID: {UserId}")]
    private static partial void LogRoleAssignedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
