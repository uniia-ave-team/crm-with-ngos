using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="ActivateUserCommand"/> to restore the business user and unlock the identity account.
/// </summary>
public partial class ActivateUserCommandHandler(
    IUserRepository userRepository,
    ILogger<ActivateUserCommandHandler> logger) : IRequestHandler<ActivateUserCommand>
{
    public async Task Handle(ActivateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.CurrentUserId)
        {
            LogCannotActivateSelf(logger, request.UserId);
            throw new InvalidOperationException("A user cannot activate their own account.");
        }

        LogActivatingUser(logger, request.UserId);

        var user = await userRepository.GetAsync(request.UserId, cancellationToken);

        if (user.IsActive)
        {
            LogUserAlreadyActive(logger, request.UserId);
            return;
        }

        await userRepository.SetUserActivationStatusAsync(user.Id, isActive: true, cancellationToken);

        LogUserActivatedSuccessfully(logger, request.UserId);
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
