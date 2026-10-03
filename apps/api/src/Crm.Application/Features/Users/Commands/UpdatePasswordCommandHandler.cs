using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdatePasswordCommand"/> to securely change a user's password.
/// </summary>
/// <param name="identityService">The identity service used for user and role management.</param>
/// <param name="logger">The logger used to record the password update process.</param>
public partial class UpdatePasswordCommandHandler(
    IIdentityService identityService,
    ILogger<UpdatePasswordCommandHandler> logger) : IRequestHandler<UpdatePasswordCommand>
{
    public async Task Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingPassword(logger, request.UserId);

        await identityService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword, cancellationToken);

        LogPasswordUpdatedSuccessfully(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingPassword, Level = LogLevel.Information, Message = "Initiating password update for user ID: {UserId}")]
    private static partial void LogUpdatingPassword(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordUpdatedSuccessfully, Level = LogLevel.Information, Message = "Password successfully updated for user ID: {UserId}")]
    private static partial void LogPasswordUpdatedSuccessfully(ILogger logger, Guid userId);
}
