using Crm.Application.Common.Consts;
using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
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
    ILogger<DeleteUserCommandHandler> logger) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        LogDeletingUser(logger, request.UserId);

        await EnsureUserIsNotLastAdminAsync(request.UserId, cancellationToken);

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

    [LoggerMessage(EventId = LogEventIds.DeletingUser, Level = LogLevel.Information, Message = "Starting GDPR hard-delete for user ID: {UserId}")]
    private static partial void LogDeletingUser(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.CannotDeleteLastAdmin, Level = LogLevel.Warning, Message = "Attempted to delete user ID: {UserId}, but they are the last system administrator.")]
    private static partial void LogCannotDeleteLastAdmin(ILogger logger, Guid userId);

    [LoggerMessage(EventId = LogEventIds.UserDeletedSuccessfully, Level = LogLevel.Information, Message = "Successfully hard-deleted user ID: {UserId} and all associated personal data.")]
    private static partial void LogUserDeletedSuccessfully(ILogger logger, Guid userId);
}
