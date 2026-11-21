using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
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
public partial class UpdateTaskCommandHandler(
    ITopicRepository topicRepository,
    ITagRepository tagRepository,
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ITaskAssigneeValidator assigneeValidator,
    TimeProvider timeProvider,
    ILogger<UpdateTaskCommandHandler> logger) : IRequestHandler<UpdateTaskCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogUpdatingTask(logger, request.TaskId, request.ProjectId);

        var projectAccess = await projectAccessService.GetRequiredAsync(request.ProjectId, cancellationToken);
        await topicRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TopicId, cancellationToken);

        var task = await taskRepository.GetByProjectAndIdForUpdateAsync(request.ProjectId, request.TaskId, cancellationToken);

        task.TopicId = request.TopicId;
        task.Title = request.Title.Trim();
        task.Description = request.Description;
        task.Priority = request.Priority;
        task.StartDate = request.StartDate;

        if (request.ExternalLinks is not null)
        {
            task.ExternalLinks = request.ExternalLinks.NormalizeExternalLinks();
        }

        if (request.Metadata is not null)
        {
            task.Metadata = (JsonObject)request.Metadata.DeepClone();
        }

        task.ChangeStatus(request.Status, request.EndDate, utcNow, updateEndDateWhenNull: true);

        if (request.AssigneeIds is not null)
        {
            await assigneeValidator.ValidateAsync(request.AssigneeIds, request.ProjectId, projectAccess, cancellationToken);
            await taskRepository.SyncTaskUsersAsync(task.Id, request.AssigneeIds, cancellationToken);
        }

        if (request.TagIds is not null)
        {
            await tagRepository.EnsureAllExistInProjectAsync(request.ProjectId, request.TagIds, cancellationToken);
            await tagRepository.SyncTaskTagsAsync(task.Id, request.TagIds, cancellationToken);
        }

        LogTaskUpdatedSuccessfully(logger, task.Id, request.ProjectId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTask, Level = LogLevel.Information, Message = "Updating task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogUpdatingTask(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskUpdatedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} updated in project ID: {ProjectId}")]
    private static partial void LogTaskUpdatedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
