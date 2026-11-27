using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AssignUserRoleCommand"/> to assign a specific Identity Role to a user
/// and invalidate their previously issued access tokens so the updated role set takes effect immediately on refresh.
/// </summary>
/// <param name="roleRepository">The repository used to retrieve role details, such as the role name required for assignment.</param>
/// <param name="identityService">The service used to manage user identities and execute the role assignment.</param>
/// <param name="revokedUsersCache">The cache used to invalidate stale access tokens issued before this role change.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role assignment process.</param>
public sealed partial class AssignUserRoleCommandHandler(
    IAuthRoleRepository roleRepository,
    IIdentityService identityService,
    IRevokedUsersCache revokedUsersCache,
    ILogger<AssignUserRoleCommandHandler> logger) : ICommandHandler<AssignUserRoleCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(AssignUserRoleCommand command, CancellationToken cancellationToken)
    {
        LogAssigningRole(logger, command.RoleId, command.UserId);

        var role = await roleRepository.GetAsync<RoleUserDto>(command.RoleId, cancellationToken);

        await identityService.AddToRoleAsync(command.UserId, role.Name, cancellationToken);
        await revokedUsersCache.RevokeTokensIssuedBeforeAsync(command.UserId, cancellationToken);

        LogRoleAssignedSuccessfully(logger, command.RoleId, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.AssigningRole, Level = LogLevel.Information, Message = "Initiating assignment of role ID '{RoleId}' to user ID: {UserId}")]
    private static partial void LogAssigningRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RoleAssignedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully assigned to user ID: {UserId}")]
    private static partial void LogRoleAssignedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
