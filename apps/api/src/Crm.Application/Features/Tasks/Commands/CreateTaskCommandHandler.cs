using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Commands;

/// <summary>
/// Handles the <see cref="CreateTaskCommand"/> to create a new task within a project.
/// </summary>
/// <param name="topicRepository">The repository for verifying topic existence in the project.</param>
/// <param name="tagRepository">The repository for resolving project-scoped tags.</param>
/// <param name="taskRepository">The repository for persisting project task entities and assignee links.</param>
/// <param name="projectAccessService">The scoped service resolving cached project access metadata.</param>
/// <param name="assigneeValidator">The validator verifying task assignee eligibility.</param>
/// <param name="timeProvider">The time provider for resolving the current UTC timestamp.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class CreateTaskCommandHandler(
    ITopicRepository topicRepository,
    ITagRepository tagRepository,
    IProjectTaskRepository taskRepository,
    IProjectAccessService projectAccessService,
    ITaskAssigneeValidator assigneeValidator,
    TimeProvider timeProvider,
    ILogger<CreateTaskCommandHandler> logger) : IRequestHandler<CreateTaskCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTaskCommand request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        LogCreatingTask(logger, request.ProjectId);

        var projectAccess = await projectAccessService.GetRequiredAsync(request.ProjectId, cancellationToken);
        await topicRepository.EnsureExistsInProjectAsync(request.ProjectId, request.TopicId, cancellationToken);

        var assigneeIds = request.AssigneeIds ?? [];
        await assigneeValidator.ValidateAsync(assigneeIds, request.ProjectId, projectAccess, cancellationToken);

        var tagIds = request.TagIds ?? [];
        await tagRepository.EnsureAllExistInProjectAsync(request.ProjectId, tagIds, cancellationToken);

        var task = new ProjectTask
        {
            ProjectId = request.ProjectId,
            TopicId = request.TopicId,
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = request.Priority,
            StartDate = request.StartDate,
            ExternalLinks = request.ExternalLinks.NormalizeExternalLinks(),
            Metadata = request.Metadata is null ? [] : (JsonObject)request.Metadata.DeepClone(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
        };

        task.ChangeStatus(request.Status, request.EndDate, utcNow, updateEndDateWhenNull: true);

        await taskRepository.CreateAsync(task, cancellationToken);
        await taskRepository.AssignToTaskAsync(task.Id, assigneeIds, cancellationToken);
        await tagRepository.AddToTaskAsync(task.Id, tagIds, cancellationToken);

        LogTaskCreatedSuccessfully(logger, task.Id, request.ProjectId);

        return task.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTask, Level = LogLevel.Information, Message = "Creating task in project ID: {ProjectId}")]
    private static partial void LogCreatingTask(ILogger logger, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskCreatedSuccessfully, Level = LogLevel.Information, Message = "Task ID: {TaskId} created in project ID: {ProjectId}")]
    private static partial void LogTaskCreatedSuccessfully(ILogger logger, Guid taskId, Guid projectId);
}
