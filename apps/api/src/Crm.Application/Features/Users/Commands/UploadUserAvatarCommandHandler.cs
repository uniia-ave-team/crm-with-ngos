using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="UploadUserAvatarCommand"/> to upload a new profile image for a specific user,
/// replace the old one on the disk, and update the user entity.
/// </summary>
/// <param name="userRepository">The repository used for accessing and updating the user record.</param>
/// <param name="fileStorageService">The service responsible for physical file I/O operations.</param>
/// <param name="fileTransactionTracker">The service responsible for tracking file transactions to ensure consistency and rollback in case of failures.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class UploadUserAvatarCommandHandler(
    IUserRepository userRepository,
    IFileStorageService fileStorageService,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<UploadUserAvatarCommandHandler> logger) : ICommandHandler<UploadUserAvatarCommand>
{
    public async ValueTask<Unit> Handle(UploadUserAvatarCommand command, CancellationToken cancellationToken)
    {
        LogUploadingUserAvatar(logger, command.UserId, command.OriginalFileName);

        var user = await userRepository.GetForUpdateAsync(command.UserId, cancellationToken);

        string newFileName = await fileStorageService.SaveImageAsync(
            command.ContentStream,
            command.OriginalFileName,
            FileStorageConstants.UsersFolder,
            cancellationToken);

        fileTransactionTracker.RegisterCreatedFile(newFileName, FileStorageConstants.UsersFolder);

        if (!string.IsNullOrWhiteSpace(user.Avatar))
        {
            fileTransactionTracker.RegisterFileForDeletion(user.Avatar, FileStorageConstants.UsersFolder);
        }

        user.Avatar = newFileName;

        LogUserAvatarUploadedSuccessfully(logger, command.UserId, newFileName);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UploadingUserAvatar, Level = LogLevel.Information, Message = "Initiating avatar upload for user ID: {UserId}. Original file name: {OriginalFileName}")]
    private static partial void LogUploadingUserAvatar(ILogger logger, Guid userId, string originalFileName);

    [LoggerMessage(EventId = LogEventIds.UserAvatarUploadedSuccessfully, Level = LogLevel.Information, Message = "User avatar successfully uploaded and updated for user ID: {UserId}. New file name: {FileName}")]
    private static partial void LogUserAvatarUploadedSuccessfully(ILogger logger, Guid userId, string fileName);
}
