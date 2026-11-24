using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Constants;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="DeleteUserCommand"/> to perform a complete GDPR hard-delete
/// of the authentication account, business profile, and all cascading personal data.
/// </summary>
public sealed partial class DeleteUserCommandHandler(
    IIdentityService identityService,
    IAuthRoleRepository roleRepository,
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IFileTransactionTracker fileTransactionTracker,
    IRevokedUsersCache revokedUsersCache,
    ILogger<DeleteUserCommandHandler> logger) : ICommandHandler<DeleteUserCommand>
{
    public async ValueTask<Unit> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        LogDeletingUser(logger, command.UserId);

        await EnsureUserIsNotLastAdminAsync(command.UserId, cancellationToken);
        await HandleSoleManagedProjectsAsync(command.UserId, command.CurrentUserId, cancellationToken);

        await TryDeleteUserAvatarAsync(command.UserId, cancellationToken);

        await identityService.DeleteUserAsync(command.UserId, cancellationToken);
        await revokedUsersCache.RevokeUserAsync(command.UserId, cancellationToken);

        LogUserDeletedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    /// <summary>
    /// Ensures that the user being deleted is not the last remaining system administrator.
    /// </summary>
    private async Task EnsureUserIsNotLastAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await roleRepository.IsUserLastAdminAsync(userId, cancellationToken))
        {
            LogCannotDeleteLastAdmin(logger, userId);
            throw new DomainValidationException("Cannot delete the user because they are the last system administrator.");
        }
    }

    /// <summary>
    /// Transfers management of any solely-managed projects to the acting administrator when deleting another user,
    /// or blocks self-deletion when no acting administrator can take over.
    /// </summary>
    private async Task HandleSoleManagedProjectsAsync(Guid userId, Guid? currentUserId, CancellationToken cancellationToken)
    {
        var soleManagedProjectIds = await projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, cancellationToken);
        if (soleManagedProjectIds.Count == 0)
        {
            return;
        }

        if (currentUserId is { } actingAdminUserId && actingAdminUserId != Guid.Empty && actingAdminUserId != userId)
        {
            await projectRepository.EnsureManagerForProjectsAsync(soleManagedProjectIds, actingAdminUserId, cancellationToken);
            LogTransferredSoleManagedProjects(logger, soleManagedProjectIds.Count, userId, actingAdminUserId);
            return;
        }

        LogCannotDeleteSoleProjectManager(logger, userId, soleManagedProjectIds.Count);
        throw new CannotRemoveLastProjectManagerException(userId, soleManagedProjectIds);
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
        catch (NotFoundException)
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

    [LoggerMessage(EventId = LogEventIds.CannotDeleteSoleProjectManager, Level = LogLevel.Warning, Message = "Attempted to delete user ID: {UserId}, but they are the sole active manager of {ProjectCount} project(s).")]
    private static partial void LogCannotDeleteSoleProjectManager(ILogger logger, Guid userId, int projectCount);

    [LoggerMessage(EventId = LogEventIds.TransferredSoleManagedProjectsOnDeletion, Level = LogLevel.Information, Message = "Transferred management of {ProjectCount} solely-managed project(s) from deleted user ID: {UserId} to acting administrator ID: {ActingAdminUserId}.")]
    private static partial void LogTransferredSoleManagedProjects(ILogger logger, int projectCount, Guid userId, Guid actingAdminUserId);
}
