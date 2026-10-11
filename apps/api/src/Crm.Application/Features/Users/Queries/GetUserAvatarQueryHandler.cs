using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Common;
using Crm.Application.Dtos.User.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Handles the <see cref="GetUserAvatarQuery"/> to retrieve the avatar file stream for a specific user.
/// </summary>
/// <param name="userRepository">The repository used for accessing the user record and avatar filename.</param>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetUserAvatarQueryHandler(
    IUserRepository userRepository,
    IFileStorageService fileStorageService,
    ILogger<GetUserAvatarQueryHandler> logger) : IRequestHandler<GetUserAvatarQuery, FileDto>
{
    public async Task<FileDto> Handle(GetUserAvatarQuery request, CancellationToken cancellationToken)
    {
        LogFetchingUserAvatar(logger, request.UserId);

        var avatar = await userRepository.GetAvatarAsync(request.UserId, cancellationToken);

        var stream = await fileStorageService.GetFileAsync(avatar, FileStorageConstants.UsersFolder, cancellationToken);

        LogUserAvatarFetched(logger, request.UserId);

        return new(stream, avatar);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingUserAvatar, Level = LogLevel.Debug, Message = "Fetching avatar file for user ID: {UserId}")]
    private static partial void LogFetchingUserAvatar(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAvatarFetched, Level = LogLevel.Debug, Message = "Avatar file query completed successfully for user ID: {UserId}")]
    private static partial void LogUserAvatarFetched(ILogger logger, Guid userId);
}
