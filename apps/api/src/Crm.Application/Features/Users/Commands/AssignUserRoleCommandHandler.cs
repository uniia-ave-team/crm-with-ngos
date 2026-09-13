using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="AssignUserRoleCommand"/> to assign a specific Identity Role to a user.
/// </summary>
/// <param name="userManager">The ASP.NET Core Identity user manager used for user retrieval and role assignment.</param>
/// <param name="roleManager">The ASP.NET Core Identity role manager used to validate and retrieve the requested role.</param>
/// <param name="logger">The logger used to record the lifecycle and outcome of the role assignment process.</param>
public partial class AssignUserRoleCommandHandler(
    UserManager<AuthUser> userManager,
    RoleManager<AuthRole> roleManager,
    ILogger<AssignUserRoleCommandHandler> logger) : IRequestHandler<AssignUserRoleCommand>
{
    public async Task Handle(AssignUserRoleCommand request, CancellationToken cancellationToken)
    {
        LogAssigningRole(logger, request.RoleId, request.UserId);

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

        var result = await userManager.AddToRoleAsync(user, role.Name!);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogRoleAssignmentFailed(logger, request.RoleId, request.UserId, errors);

            throw new InvalidOperationException($"Failed to assign role to user: {errors}");
        }

        LogRoleAssignedSuccessfully(logger, request.RoleId, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.AssigningRole, Level = LogLevel.Information, Message = "Initiating assignment of role ID '{RoleId}' to user ID: {UserId}")]
    private static partial void LogAssigningRole(ILogger logger, Guid roleId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserNotFound, Level = LogLevel.Warning, Message = "Role assignment failed. User with ID {UserId} was not found.")]
    private static partial void LogUserNotFound(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.AssignUserRoleCommandHandlerRoleNotFound, Level = LogLevel.Warning, Message = "Role assignment failed. Role with ID {RoleId} was not found.")]
    private static partial void LogRoleNotFound(ILogger logger, Guid roleId);

    [LoggerMessage(EventId = LogEventIds.RoleAssignmentFailed, Level = LogLevel.Warning, Message = "Failed to assign role ID '{RoleId}' to user ID {UserId}. Reason: {Errors}")]
    private static partial void LogRoleAssignmentFailed(ILogger logger, Guid roleId, Guid userId, string errors);

    [LoggerMessage(EventId = LogEventIds.RoleAssignedSuccessfully, Level = LogLevel.Information, Message = "Role ID '{RoleId}' successfully assigned to user ID: {UserId}")]
    private static partial void LogRoleAssignedSuccessfully(ILogger logger, Guid roleId, Guid userId);
}
