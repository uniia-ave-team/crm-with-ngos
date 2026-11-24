using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Roles;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RemoveUserRoleCommand"/> to remove a specific Identity Role from a user.
/// </summary>
/// <param name="identityService">The identity service used for user and role management.</param>
/// <param name="authRoleRepository">The repository for managing authentication roles.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role removal process.</param>
public sealed partial class RemoveUserRoleCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository authRoleRepository,
    ILogger<RemoveUserRoleCommandHandler> logger) : ICommandHandler<RemoveUserRoleCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(RemoveUserRoleCommand command, CancellationToken cancellationToken)
    {
        LogRemovingRole(logger, command.RoleId, command.UserId);

        var role = await authRoleRepository.GetAsync<RoleUserDto>(command.RoleId, cancellationToken);

        if (string.Equals(role.Name, RoleConstants.Admin, StringComparison.OrdinalIgnoreCase) &&
            await authRoleRepository.IsUserLastAdminAsync(command.UserId, cancellationToken))
        {
            throw new DomainValidationException("Cannot remove the Administrator role from the last active administrator.");
        }

        await identityService.RemoveFromRoleAsync(command.UserId, role.Name, cancellationToken);

        LogRoleRemovedSuccessfully(logger, command.RoleId, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RemovingRole, Level = LogLevel.Information, Message = "Initiating removal of role ID '{RoleId}' from user ID: {UserId}")]
    private static partial void LogRemovingRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RoleRemovedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully removed from user ID: {UserId}")]
    private static partial void LogRoleRemovedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
