using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="DeactivateUserCommand"/> to soft-delete the business user and lock the identity account.
/// </summary>
public partial class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    ILogger<DeactivateUserCommandHandler> logger) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.CurrentUserId)
        {
            LogCannotDeactivateSelf(logger, request.UserId);
            throw new InvalidOperationException("A user cannot deactivate their own account.");
        }

        LogDeactivatingUser(logger, request.UserId);

        var user = await userRepository.GetAsync(request.UserId, cancellationToken);

        if (!user.IsActive)
        {
            LogUserAlreadyDeactivated(logger, request.UserId);
            return;
        }

        await EnsureUserIsNotLastAdminAsync(request.UserId, cancellationToken);

        user.IsActive = false;

        userRepository.Update(user);

        LogUserDeactivatedSuccessfully(logger, request.UserId);
    }

    /// <summary>
    /// Ensures that the user being deactivated is not the last remaining system administrator.
    /// </summary>
    private async Task EnsureUserIsNotLastAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        var isLastAdmin = await roleRepository.IsUserLastAdminAsync(userId, cancellationToken);

        if (isLastAdmin)
        {
            LogCannotDeactivateLastAdmin(logger, userId);
            throw new InvalidOperationException("Cannot deactivate the user because they are the last system administrator.");
        }
    }

    [LoggerMessage(EventId = LogEventIds.DeactivatingUser, Level = LogLevel.Information, Message = "Deactivating user ID: {UserId}")]
    private static partial void LogDeactivatingUser(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAlreadyDeactivated, Level = LogLevel.Warning, Message = "User ID: {UserId} is already deactivated.")]
    private static partial void LogUserAlreadyDeactivated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserDeactivatedSuccessfully, Level = LogLevel.Information, Message = "Successfully deactivated user ID: {UserId} (Profile soft-deleted and Identity locked).")]
    private static partial void LogUserDeactivatedSuccessfully(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeactivateSelf, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to deactivate their own account.")]
    private static partial void LogCannotDeactivateSelf(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeactivateLastAdmin, Level = LogLevel.Warning, Message = "Failed to deactivate user ID: {UserId}. Reason: User is the last active system administrator.")]
    private static partial void LogCannotDeactivateLastAdmin(ILogger logger, Guid userId);
}
