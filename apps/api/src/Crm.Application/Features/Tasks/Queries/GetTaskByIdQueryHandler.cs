using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Tasks;
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
/// <param name="fileUrlProvider">The file URL provider for resolving assignee avatars and attachment download URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetTaskByIdQueryHandler(
    IProjectTaskRepository taskRepository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskByIdQueryHandler> logger) : IQueryHandler<GetTaskByIdQuery, TaskDto>
{
    /// <inheritdoc />
    public async ValueTask<TaskDto> Handle(GetTaskByIdQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTaskById(logger, query.TaskId, query.ProjectId);

        var task = await taskRepository.GetByProjectAndIdAsync<TaskDto>(query.ProjectId, query.TaskId, cancellationToken);
        var unreadCounts = await taskRepository.GetUnreadCommentCountsAsync([task.Id], query.CurrentUserId, cancellationToken);

        LogTaskByIdFetched(logger, query.TaskId, query.ProjectId);

        return task.Enrich(fileUrlProvider, query.CurrentUserId, unreadCounts.GetValueOrDefault(task.Id));
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskById, Level = LogLevel.Information, Message = "Fetching task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskById(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskByIdFetched, Level = LogLevel.Information, Message = "Task ID: {TaskId} fetched from project ID: {ProjectId}")]
    private static partial void LogTaskByIdFetched(ILogger logger, Guid taskId, Guid projectId);
}
