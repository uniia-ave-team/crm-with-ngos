using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="UpdateTaskCommand"/> to update a task's properties, status, dates, external links, metadata, assignees, and tags.
/// </summary>
/// <param name="topicRepository">The repository for verifying topic existence in the project.</param>
/// <param name="tagRepository">The repository for resolving project-scoped tags.</param>
/// <param name="taskRepository">The repository for retrieving and updating project task entities and assignee links.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="assigneeValidator">The validator verifying task assignee eligibility.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class UpdateTaskCommandHandler(
    ITopicRepository topicRepository,
    ITagRepository tagRepository,
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ITaskAssigneeValidator assigneeValidator,
    TimeProvider timeProvider,
    ILogger<UpdateTaskCommandHandler> logger) : ICommandHandler<UpdateTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTaskCommand command, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUpdatingTask(logger, command.TaskId, command.ProjectId);

        var projectAccess = await projectAccessService.GetRequiredAsync(command.ProjectId, cancellationToken);
        var task = await taskRepository.GetByProjectAndIdForUpdateAsync(command.ProjectId, command.TaskId, cancellationToken);

        await topicRepository.EnsureExistsInProjectAsync(command.ProjectId, command.TopicId, cancellationToken);

        task.TopicId = command.TopicId;
        task.Title = command.Title.Trim();
        task.Description = command.Description;
        task.Priority = command.Priority;
        task.StartDate = command.StartDate;

        if (command.ExternalLinks is not null)
        {
            task.ReplaceExternalLinks(command.ExternalLinks.NormalizeExternalLinks());
        }

        if (command.Metadata is not null)
        {
            task.Metadata = (JsonObject)command.Metadata.DeepClone();
        }

        task.ApplyFullStatusUpdate(command.Status, command.EndDate, utcNow);

        if (command.AssigneeIds is not null)
        {
            await assigneeValidator.ValidateAsync(command.AssigneeIds, command.ProjectId, projectAccess, cancellationToken);
            await taskRepository.SyncTaskUsersAsync(task.Id, command.AssigneeIds, cancellationToken);
        }

        if (command.TagIds is not null)
        {
            await tagRepository.EnsureAllExistInProjectAsync(command.ProjectId, command.TagIds, cancellationToken);
            await tagRepository.SyncTaskTagsAsync(task.Id, command.TagIds, cancellationToken);
        }

        LogTaskUpdatedSuccessfully(logger, task.Id, command.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTask, Level = LogLevel.Information, Message = "Updating task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskUpdatedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} updated in project ID: {ProjectId}")]
    private static partial void LogTaskUpdatedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
