using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdateUserProfileCommand"/> to modify user profile details.
/// </summary>
public sealed partial class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    ILogger<UpdateUserProfileCommandHandler> logger) : ICommandHandler<UpdateUserProfileCommand>
{
    public async ValueTask<Unit> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingUserProfile(logger, command.UserId);

        var user = await userRepository.GetForUpdateAsync(command.UserId, cancellationToken);

        command.Adapt(user);

        LogUserProfileUpdatedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserProfile, Level = LogLevel.Information, Message = "Updating profile for user ID: {UserId}")]
    private static partial void LogUpdatingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated profile for user ID: {UserId}")]
    private static partial void LogUserProfileUpdatedSuccessfully(ILogger logger, Guid userId);
}
