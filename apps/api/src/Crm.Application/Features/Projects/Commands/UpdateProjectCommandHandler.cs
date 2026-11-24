using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Commands;

/// <summary>
/// Handles the <see cref="UpdateProjectCommand"/> to update an existing project's details and visibility.
/// </summary>
/// <param name="projectRepository">The repository for retrieving and updating project entities.</param>
/// <param name="taskRepository">The repository for cleaning up inaccessible task assignments and read states when restricting visibility.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UpdateProjectCommandHandler(
    IProjectRepository projectRepository,
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ILogger<UpdateProjectCommandHandler> logger) : ICommandHandler<UpdateProjectCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateProjectCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingProject(logger, command.Id, command.CurrentUserId);

        await projectRepository.LockForUpdateAsync(command.Id, cancellationToken);
        var project = await projectRepository.GetForUpdateAsync(command.Id, cancellationToken);
        var previousVisibility = project.Visibility;

        if (previousVisibility != command.Visibility)
        {
            var access = await projectAccessService.GetRequiredAsync(project.Id, cancellationToken);
            access.EnsureHasClaim(project.Id, ProjectClaim.Manage);
        }

        command.Adapt(project);
        project.EnsureValidDateRange();

        if (previousVisibility != ProjectVisibility.Private && command.Visibility == ProjectVisibility.Private)
        {
            await taskRepository.RemoveInaccessibleUserDataAsync(project.Id, cancellationToken);
        }

        LogProjectUpdatedSuccessfully(logger, project.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingProject, Level = LogLevel.Information, Message = "Updating project ID: {ProjectId} by user ID: {UserId}")]
    private static partial void LogUpdatingProject(ILogger logger, Guid projectId, Guid userId);

    [LoggerMessage(EventId = LogEventIds.ProjectUpdatedSuccessfully, Level = LogLevel.Information, Message = "Project ID: {ProjectId} updated successfully.")]
    private static partial void LogProjectUpdatedSuccessfully(ILogger logger, Guid projectId);
}
