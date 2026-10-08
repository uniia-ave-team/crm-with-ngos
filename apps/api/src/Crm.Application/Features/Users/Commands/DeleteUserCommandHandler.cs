using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="DeleteUserCommand"/> to perform a complete GDPR hard-delete
/// of the authentication account, business profile, and all cascading personal data.
/// </summary>
public partial class DeleteUserCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository roleRepository,
    IUserRepository userRepository,
    IFileTransactionTracker fileTransactionTracker,
    ILogger<DeleteUserCommandHandler> logger) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        LogDeletingUser(logger, request.UserId);

        await EnsureUserIsNotLastAdminAsync(request.UserId, cancellationToken);

        await TryDeleteUserAvatarAsync(request.UserId, cancellationToken);

        await identityService.DeleteUserAsync(request.UserId, cancellationToken);

        LogUserDeletedSuccessfully(logger, request.UserId);
    }

    /// <summary>
    /// Ensures that the user being deleted is not the last remaining system administrator.
    /// </summary>
    private async Task EnsureUserIsNotLastAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await roleRepository.IsUserLastAdminAsync(userId, cancellationToken))
        {
            LogCannotDeleteLastAdmin(logger, userId);
            throw new InvalidOperationException("Cannot delete the user because they are the last system administrator.");
        }
    }

    /// <summary>
    /// Safely attempts to retrieve and delete a user's avatar file from storage.
    /// Encapsulates exception handling for missing avatars or users.
    /// </summary>
    private async Task TryDeleteUserAvatarAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var avatar = await userRepository.GetAvatarAsync(userId, cancellationToken);
            fileTransactionTracker.RegisterFileForDeletion(avatar, FileStorageConstants.UsersFolder);

            LogUserAvatarDeletedSuccessfully(logger, userId);
        }
        catch (EntityFieldNotFoundException)
        {
            LogUserAvatarNotFound(logger, userId);
        }
    }

    [LoggerMessage(EventId = LogEventIds.DeletingUser, Level = LogLevel.Information, Message = "Starting GDPR hard-delete for user ID: {UserId}")]
    private static partial void LogDeletingUser(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeleteLastAdmin, Level = LogLevel.Warning, Message = "Attempted to delete user ID: {UserId}, but they are the last system administrator.")]
    private static partial void LogCannotDeleteLastAdmin(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAvatarDeletedSuccessfully, Level = LogLevel.Information, Message = "Successfully deleted avatar file for user ID: {UserId}")]
    private static partial void LogUserAvatarDeletedSuccessfully(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAvatarNotFound, Level = LogLevel.Debug, Message = "No avatar found in database for user ID: {UserId}, skipping disk deletion.")]
    private static partial void LogUserAvatarNotFound(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserDeletedSuccessfully, Level = LogLevel.Information, Message = "Successfully hard-deleted user ID: {UserId} and all associated personal data.")]
    private static partial void LogUserDeletedSuccessfully(ILogger logger, Guid userId);
}
