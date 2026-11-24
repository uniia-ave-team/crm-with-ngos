using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="RemoveProjectMemberCommand"/> to remove a user from a project,
/// protecting the last remaining active project manager and cleaning up task assignments/read states
/// when the user loses access to a private project.
/// </summary>
/// <param name="projectRepository">The repository for verifying manager retention and removing project members.</param>
/// <param name="taskRepository">The repository for cleaning up task assignments and read states for users who lose access.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class RemoveProjectMemberCommandHandler(
    IProjectRepository projectRepository,
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ILogger<RemoveProjectMemberCommandHandler> logger) : ICommandHandler<RemoveProjectMemberCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(RemoveProjectMemberCommand command, CancellationToken cancellationToken)
    {
        LogRemovingProjectMember(logger, command.UserId, command.ProjectId);

        await projectRepository.LockForUpdateAsync(command.ProjectId, cancellationToken);

        ProjectClaim memberClaim = await projectRepository.GetMemberClaimAsync(command.ProjectId, command.UserId, cancellationToken)
            ?? throw new EntityNotFoundException<ProjectMember>(command.UserId);

        if (memberClaim >= ProjectClaim.Manage
            && !await projectRepository.HasAnotherManagerAsync(command.ProjectId, command.UserId, cancellationToken))
        {
            throw new CannotRemoveLastProjectManagerException(command.ProjectId);
        }

        await projectRepository.RemoveMemberAsync(command.ProjectId, command.UserId, cancellationToken);

        var projectAccess = await projectAccessService.GetRequiredAsync(command.ProjectId, cancellationToken);
        if (projectAccess.Visibility == ProjectVisibility.Private)
        {
            await taskRepository.RemoveInaccessibleUserDataAsync(command.ProjectId, cancellationToken);
        }

        LogProjectMemberRemoved(logger, command.UserId, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.RemovingProjectMember, Level = LogLevel.Information, Message = "Removing project member user ID: {UserId} from project ID: {ProjectId}")]
    private static partial void LogRemovingProjectMember(ILogger logger, Guid userId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.ProjectMemberRemoved, Level = LogLevel.Information, Message = "Project member user ID: {UserId} removed from project ID: {ProjectId}")]
    private static partial void LogProjectMemberRemoved(ILogger logger, Guid userId, Guid projectId);
}
