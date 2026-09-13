using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdatePasswordCommand"/> to securely change a user's password.
/// </summary>
/// <param name="userManager">The ASP.NET Core Identity user manager used for password operations.</param>
/// <param name="logger">The logger used to record the password update process.</param>
public partial class UpdatePasswordCommandHandler(
    UserManager<AuthUser> userManager,
    ILogger<UpdatePasswordCommandHandler> logger) : IRequestHandler<UpdatePasswordCommand>
{
    public async Task Handle(UpdatePasswordCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingPassword(logger, request.UserId);

        var user = await userManager.FindByIdAsync(request.UserId.ToString())
            ?? throw new EntityNotFoundException("User", request.UserId);

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

        if (!result.Succeeded)
        {
            string errors = string.Join(" | ", result.Errors.Select(e => e.Description));
            LogPasswordUpdateFailed(logger, request.UserId, errors);
            throw new InvalidOperationException($"Password update failed: {errors}");
        }

        LogPasswordUpdatedSuccessfully(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingPassword, Level = LogLevel.Information, Message = "Initiating password update for user ID: {UserId}")]
    private static partial void LogUpdatingPassword(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.PasswordUpdateFailed, Level = LogLevel.Warning, Message = "Failed to update password for user ID '{UserId}'. Reason: {Errors}")]
    private static partial void LogPasswordUpdateFailed(ILogger logger, Guid userId, string errors);

    [LoggerMessage(EventId = LogEventIds.PasswordUpdatedSuccessfully, Level = LogLevel.Information, Message = "Password successfully updated for user ID: {UserId}")]
    private static partial void LogPasswordUpdatedSuccessfully(ILogger logger, Guid userId);
}
