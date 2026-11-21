using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Handles the <see cref="GetProjectTasksQuery"/> to return a paginated, filtered, and sorted list of task summaries
/// either for a specific project or globally across all visible projects.
/// </summary>
/// <param name="taskRepository">The repository for querying project tasks and unread comment counts.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving assignee avatars.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetProjectTasksQueryHandler(
    IProjectTaskRepository taskRepository,
    ICurrentUserService currentUserService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetProjectTasksQueryHandler> logger) : IRequestHandler<GetProjectTasksQuery, PagedResult<TaskSummaryDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TaskSummaryDto>> Handle(GetProjectTasksQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingProjectTasks(logger, request.ProjectId, request.Filter, request.PageNumber, request.PageSize);

        var filter = request.Adapt<ProjectTaskFilter>();
        var pagedTasks = await taskRepository.GetPagedAsync<TaskSummaryDto>(
            filter: filter,
            currentUserId: currentUserId,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        var unreadCounts = await taskRepository.GetUnreadCommentCountsAsync(
            pagedTasks.Items.Select(t => t.Id),
            currentUserId,
            cancellationToken);

        var enriched = pagedTasks.Items.Enrich(fileUrlProvider, currentUserId, unreadCounts);

        LogProjectTasksFetched(logger, enriched.Count, pagedTasks.TotalCount, request.ProjectId);

        return new PagedResult<TaskSummaryDto>(
            enriched,
            pagedTasks.TotalCount,
            pagedTasks.PageNumber,
            pagedTasks.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjectTasks, Level = LogLevel.Information, Message = "Fetching tasks for project ID: {ProjectId} (Filter: {Filter}, Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingProjectTasks(ILogger logger, Guid? projectId, string? filter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.ProjectTasksFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} tasks for project ID: {ProjectId}")]
    private static partial void LogProjectTasksFetched(ILogger logger, int count, int totalCount, Guid? projectId);
}
