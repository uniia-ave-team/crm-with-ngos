using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdatePasswordCommand"/> to securely change a user's password.
/// </summary>
/// <param name="identityService">The identity service used for user and role management.</param>
/// <param name="logger">The logger used to record the password update process.</param>
public sealed partial class UpdatePasswordCommandHandler(
    IIdentityService identityService,
    ILogger<UpdatePasswordCommandHandler> logger) : ICommandHandler<UpdatePasswordCommand>
{
    public async ValueTask<Unit> Handle(UpdatePasswordCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingPassword(logger, command.UserId);

        await identityService.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword, cancellationToken);

        LogPasswordUpdatedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingPassword, Level = LogLevel.Information, Message = "Initiating password update for user ID: {UserId}")]
    private static partial void LogUpdatingPassword(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordUpdatedSuccessfully, Level = LogLevel.Information, Message = "Password successfully updated for user ID: {UserId}")]
    private static partial void LogPasswordUpdatedSuccessfully(ILogger logger, Guid userId);
}
