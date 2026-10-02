using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Role;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RemoveUserRoleCommand"/> to remove a specific Identity Role from a user.
/// </summary>
/// <param name="identityService">The identity service used for user and role management.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role removal process.</param>
public partial class RemoveUserRoleCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
    ILogger<RemoveUserRoleCommandHandler> logger) : IRequestHandler<RemoveUserRoleCommand>
{
    public async Task Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        LogRemovingRole(logger, request.RoleId, request.UserId);

        var role = await authRoleRepository.GetAsync<RoleUserDto>(request.RoleId, cancellationToken);

        await identityService.RemoveFromRoleAsync(request.UserId, role.Name, cancellationToken);

        LogRoleRemovedSuccessfully(logger, request.RoleId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.RemovingRole, Level = LogLevel.Information, Message = "Initiating removal of role ID '{RoleId}' from user ID: {UserId}")]
    private static partial void LogRemovingRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RoleRemovedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully removed from user ID: {UserId}")]
    private static partial void LogRoleRemovedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
