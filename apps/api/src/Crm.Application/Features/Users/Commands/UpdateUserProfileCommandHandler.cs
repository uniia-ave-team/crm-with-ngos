using Crm.Application.Common.Consts;
using Crm.Application.Features.Users.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdateUserProfileCommand"/> to modify user profile details
/// while preserving <see cref="Domain.Entities.User.EmergencyContact"/> when the caller lacks
/// <see cref="AccessRight.ViewEmergencyContact"/> for another user.
/// </summary>
public sealed partial class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    IPermissionService permissionService,
    ILogger<UpdateUserProfileCommandHandler> logger) : ICommandHandler<UpdateUserProfileCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingUserProfile(logger, command.UserId);

        var user = await userRepository.GetForUpdateAsync(command.UserId, cancellationToken);

        bool isSelf = command.TargetCurrentUser || command.CurrentUserId == command.UserId;
        bool isChangingEmergencyContact = !string.Equals(
            command.EmergencyContact,
            user.EmergencyContact,
            StringComparison.Ordinal);

        bool canEditEmergencyContact = isSelf
            || !isChangingEmergencyContact
            || await permissionService.HasAccessAsync(
                command.CurrentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken);

        user.ApplyProfileUpdate(command, canEditEmergencyContact);

        LogUserProfileUpdatedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserProfile, Level = LogLevel.Information, Message = "Updating profile for user ID: {UserId}")]
    private static partial void LogUpdatingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated profile for user ID: {UserId}")]
    private static partial void LogUserProfileUpdatedSuccessfully(ILogger logger, Guid userId);
}
