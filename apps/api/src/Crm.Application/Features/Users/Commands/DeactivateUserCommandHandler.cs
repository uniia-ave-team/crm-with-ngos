using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Handles the <see cref="DeactivateUserCommand"/> to soft-delete the business user and lock the identity account.
/// </summary>
public partial class DeactivateUserCommandHandler(
    IUserRepository userRepository,
    IAuthRoleRepository roleRepository,
    IProjectRepository projectRepository,
    ILogger<DeactivateUserCommandHandler> logger) : IRequestHandler<DeactivateUserCommand>
{
    public async ValueTask<Unit> Handle(DeactivateUserCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == request.CurrentUserId)
        {
            LogCannotDeactivateSelf(logger, request.UserId);
            throw new InvalidOperationException("A user cannot deactivate their own account.");
        }

        LogDeactivatingUser(logger, request.UserId);

        var user = await userRepository.GetAsync(request.UserId, cancellationToken);

        if (!user.IsActive)
        {
            LogUserAlreadyDeactivated(logger, request.UserId);
            return Unit.Value;
        }

        await EnsureUserIsNotLastAdminAsync(request.UserId, cancellationToken);
        await TransferSoleManagedProjectsAsync(request.UserId, request.CurrentUserId, cancellationToken);

        await userRepository.SetUserActivationStatusAsync(user.Id, isActive: false, cancellationToken);

        LogUserDeactivatedSuccessfully(logger, request.UserId);

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
            throw new InvalidOperationException("Cannot deactivate the user because they are the last system administrator.");
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
