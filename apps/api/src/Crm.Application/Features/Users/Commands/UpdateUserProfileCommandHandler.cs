using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UpdateUserProfileCommand"/> to modify user profile details.
/// </summary>
public partial class UpdateUserProfileCommandHandler(
    IUserRepository userRepository,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<UpdateUserProfileCommandHandler> logger) : IRequestHandler<UpdateUserProfileCommand>
{
    public async Task Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingUserProfile(logger, request.UserId);

        var user = await userRepository.GetForUpdateAsync(request.UserId, cancellationToken);

        if (request.AvatarUrl is not null && user.Avatar is not null)
        {
            fileTransactionTracker.RegisterFileForDeletion(user.Avatar, FileStorageConstants.UsersFolder);
        }

        request.Adapt(user);

        LogUserProfileUpdatedSuccessfully(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserProfile, Level = LogLevel.Information, Message = "Updating profile for user ID: {UserId}")]
    private static partial void LogUpdatingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated profile for user ID: {UserId}")]
    private static partial void LogUserProfileUpdatedSuccessfully(ILogger logger, Guid userId);
}
