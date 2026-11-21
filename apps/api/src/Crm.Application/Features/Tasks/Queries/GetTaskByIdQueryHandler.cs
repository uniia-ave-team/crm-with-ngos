using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Handles the <see cref="GetTaskByIdQuery"/> to retrieve a single task by ID within a project.
/// </summary>
/// <param name="taskRepository">The repository for querying task details and unread comment counts.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving assignee avatars and attachment download URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetTaskByIdQueryHandler(
    IProjectTaskRepository taskRepository,
    ICurrentUserService currentUserService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskByIdQueryHandler> logger) : IRequestHandler<GetTaskByIdQuery, TaskDto>
{
    /// <inheritdoc />
    public async ValueTask<TaskDto> Handle(GetTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingTaskById(logger, request.TaskId, request.ProjectId);

        var task = await taskRepository.GetByProjectAndIdAsync<TaskDto>(request.ProjectId, request.TaskId, cancellationToken);
        var unreadCounts = await taskRepository.GetUnreadCommentCountsAsync([task.Id], currentUserId, cancellationToken);

        LogTaskByIdFetched(logger, request.TaskId, request.ProjectId);

        return task.Enrich(fileUrlProvider, currentUserId, unreadCounts.GetValueOrDefault(task.Id));
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskById, Level = LogLevel.Information, Message = "Fetching task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskById(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskByIdFetched, Level = LogLevel.Information, Message = "Task ID: {TaskId} fetched from project ID: {ProjectId}")]
    private static partial void LogTaskByIdFetched(ILogger logger, Guid taskId, Guid projectId);
}
