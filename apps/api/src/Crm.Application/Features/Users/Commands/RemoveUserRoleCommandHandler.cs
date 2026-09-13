using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="RemoveUserRoleCommand"/> to remove a specific Identity Role from a user.
/// </summary>
/// <param name="userManager">The ASP.NET Core Identity user manager used for user retrieval and role removal.</param>
/// <param name="roleManager">The ASP.NET Core Identity role manager used to validate and retrieve the requested role.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role removal process.</param>
public partial class RemoveUserRoleCommandHandler(
    UserManager<AuthUser> userManager,
    RoleManager<AuthRole> roleManager,
    ILogger<RemoveUserRoleCommandHandler> logger) : IRequestHandler<RemoveUserRoleCommand>
{
    public async Task Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        LogRemovingRole(logger, request.RoleId, request.UserId);

        var user = await userManager.FindByIdAsync(request.UserId.ToString());

        if (user == null)
        {
            LogUserNotFound(logger, request.UserId);

            throw new EntityNotFoundException(nameof(AuthUser), request.UserId);
        }

        var role = await roleManager.FindByIdAsync(request.RoleId.ToString());

        if (role == null)
        {
            LogRoleNotFound(logger, request.RoleId);

            throw new EntityNotFoundException(nameof(AuthRole), request.RoleId);
        }

        var result = await userManager.RemoveFromRoleAsync(user, role.Name!);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleRemovalFailed(logger, request.RoleId, request.UserId, errors);

            throw new InvalidOperationException($"Failed to remove role from user: {errors}");
        }

        LogRoleRemovedSuccessfully(logger, request.RoleId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.RemovingRole, Level = LogLevel.Information, Message = "Initiating removal of role ID '{RoleId}' from user ID: {UserId}")]
    private static partial void LogRemovingRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RemoveUserRoleCommandHandlerUserNotFound, Level = LogLevel.Warning, Message = "Role removal failed. User with ID {UserId} was not found.")]
    private static partial void LogUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.RemoveUserRoleCommandHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Role removal failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleRemovalFailed, Level = LogLevel.Warning, Message = "Failed to remove role ID '{RoleId}' from user ID {UserId}. Reason: {Errors}")]
    private static partial void LogRoleRemovalFailed(ILogger logger, Guid roleId, Guid userId, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleRemovedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully removed from user ID: {UserId}")]
    private static partial void LogRoleRemovedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
