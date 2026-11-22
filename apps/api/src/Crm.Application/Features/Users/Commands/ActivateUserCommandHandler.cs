using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="ActivateUserCommand"/> to restore the business user,
/// unlock the identity account, and remove the user from the revoked users cache.
/// </summary>
public sealed partial class ActivateUserCommandHandler(
    IUserRepository userRepository,
    IRevokedUsersCache revokedUsersCache,
    ILogger<ActivateUserCommandHandler> logger) : ICommandHandler<ActivateUserCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(ActivateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == command.CurrentUserId)
        {
            LogCannotActivateSelf(logger, command.UserId);
            throw new DomainValidationException("A user cannot activate their own account.");
        }

        LogActivatingUser(logger, command.UserId);

        bool isActive = await userRepository.IsActiveAsync(command.UserId, cancellationToken);

        if (isActive)
        {
            LogUserAlreadyActive(logger, command.UserId);
            return Unit.Value;
        }

        await userRepository.SetUserActivationStatusAsync(command.UserId, isActive: true, cancellationToken);
        await revokedUsersCache.UnrevokeUserAsync(command.UserId, cancellationToken);

        LogUserActivatedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.ActivatingUser, Level = LogLevel.Information, Message = "Activating user ID: {UserId}")]
    private static partial void LogActivatingUser(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAlreadyActive, Level = LogLevel.Warning, Message = "User ID: {UserId} is already active.")]
    private static partial void LogUserAlreadyActive(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserActivatedSuccessfully, Level = LogLevel.Information, Message = "Successfully activated user ID: {UserId} (Profile restored and Identity unlocked).")]
    private static partial void LogUserActivatedSuccessfully(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotActivateSelf, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to activate their own account.")]
    private static partial void LogCannotActivateSelf(ILogger logger, Guid userId);
}
