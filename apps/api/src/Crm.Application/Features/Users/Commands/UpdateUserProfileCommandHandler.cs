using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
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
    ILogger<UpdateUserProfileCommandHandler> logger) : IRequestHandler<UpdateUserProfileCommand>
{
    public async Task Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingUserProfile(logger, request.UserId);

        var user = await userRepository.GetAsync(request.UserId, cancellationToken);

        request.Adapt(user);

        userRepository.Update(user);

        LogUserProfileUpdatedSuccessfully(logger, request.UserId);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingUserProfile, Level = LogLevel.Information, Message = "Updating profile for user ID: {UserId}")]
    private static partial void LogUpdatingUserProfile(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserProfileUpdatedSuccessfully, Level = LogLevel.Information, Message = "Successfully updated profile for user ID: {UserId}")]
    private static partial void LogUserProfileUpdatedSuccessfully(ILogger logger, Guid userId);
}
