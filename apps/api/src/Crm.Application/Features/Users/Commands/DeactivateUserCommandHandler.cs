using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="DeactivateUserCommand"/> to soft-delete the business user,
/// revoke active refresh tokens, and add the user to the revoked users cache.
/// </summary>
public sealed partial class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    IProjectRepository projectRepository,
    IUserRefreshTokenRepository userRefreshTokenRepository,
    IRevokedUsersCache revokedUsersCache,
    ILogger<DeactivateUserCommandHandler> logger) : ICommandHandler<DeactivateUserCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeactivateUserCommand command, CancellationToken cancellationToken)
    {
        if (command.UserId == command.CurrentUserId)
        {
            LogCannotDeactivateSelf(logger, command.UserId);
            throw new DomainValidationException("A user cannot deactivate their own account.");
        }

        LogDeactivatingUser(logger, command.UserId);

        bool isActive = await userRepository.IsActiveAsync(command.UserId, cancellationToken);

        if (!isActive)
        {
            LogUserAlreadyDeactivated(logger, command.UserId);
            return Unit.Value;
        }

        await EnsureUserIsNotLastAdminAsync(command.UserId, cancellationToken);
        await TransferSoleManagedProjectsAsync(command.UserId, command.CurrentUserId, cancellationToken);

        await userRepository.SetUserActivationStatusAsync(command.UserId, isActive: false, cancellationToken);
        await userRefreshTokenRepository.RevokeAllUserTokensAsync(command.UserId, cancellationToken);
        await revokedUsersCache.RevokeUserAsync(command.UserId, cancellationToken);

        LogUserDeactivatedSuccessfully(logger, command.UserId);

        return Unit.Value;
    }

    /// <summary>
    /// Ensures that the user being deactivated is not the last remaining system administrator.
    /// </summary>
    private async Task EnsureUserIsNotLastAdminAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (await roleRepository.IsUserLastAdminAsync(userId, cancellationToken))
        {
            LogCannotDeactivateLastAdmin(logger, userId);
            throw new DomainValidationException("Cannot deactivate the user because they are the last system administrator.");
        }
    }

    /// <summary>
    /// Transfers management of any projects where <paramref name="userId"/> is the sole active manager
    /// to the acting administrator (<paramref name="actingAdminUserId"/>) so no project becomes orphaned.
    /// </summary>
    private async Task TransferSoleManagedProjectsAsync(Guid userId, Guid actingAdminUserId, CancellationToken cancellationToken)
    {
        var soleManagedProjectIds = await projectRepository.GetSoleManagedProjectIdsForUpdateAsync(userId, cancellationToken);
        if (soleManagedProjectIds.Count > 0)
        {
            await projectRepository.EnsureManagerForProjectsAsync(soleManagedProjectIds, actingAdminUserId, cancellationToken);
            LogTransferredSoleManagedProjects(logger, soleManagedProjectIds.Count, userId, actingAdminUserId);
        }
    }

    [LoggerMessage(EventId = LogEventIds.DeactivatingUser, Level = LogLevel.Information, Message = "Deactivating user ID: {UserId}")]
    private static partial void LogDeactivatingUser(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserAlreadyDeactivated, Level = LogLevel.Warning, Message = "User ID: {UserId} is already deactivated.")]
    private static partial void LogUserAlreadyDeactivated(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserDeactivatedSuccessfully, Level = LogLevel.Information, Message = "Successfully deactivated user ID: {UserId} (Profile soft-deleted and Identity locked).")]
    private static partial void LogUserDeactivatedSuccessfully(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeactivateSelf, Level = LogLevel.Warning, Message = "User ID: {UserId} attempted to deactivate their own account.")]
    private static partial void LogCannotDeactivateSelf(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeactivateLastAdmin, Level = LogLevel.Warning, Message = "Failed to deactivate user ID: {UserId}. Reason: User is the last active system administrator.")]
    private static partial void LogCannotDeactivateLastAdmin(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.TransferredSoleManagedProjectsOnDeactivation, Level = LogLevel.Information, Message = "Transferred management of {ProjectCount} solely-managed project(s) from deactivated user ID: {UserId} to acting administrator ID: {ActingAdminUserId}.")]
    private static partial void LogTransferredSoleManagedProjects(ILogger logger, int projectCount, Guid userId, Guid actingAdminUserId);
}
