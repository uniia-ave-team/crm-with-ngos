using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Application.Dtos.Task.Queries;
using Crm.Application.Features.Tasks.Extensions;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Tasks.Queries;

/// <summary>
/// Handles the <see cref="GetTaskCommentsQuery"/> to retrieve a paginated list of comments on a task.
/// </summary>
/// <param name="commentRepository">The repository for querying task comments.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving comment author avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetTaskCommentsQueryHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskCommentsQueryHandler> logger) : IRequestHandler<GetTaskCommentsQuery, PagedResult<TaskCommentDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TaskCommentDto>> Handle(GetTaskCommentsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingTaskComments(logger, request.TaskId, request.ProjectId);

        var paged = await commentRepository.GetPagedByTaskIdAsync<TaskCommentDto>(
            request.TaskId,
            request.PageNumber,
            request.PageSize,
            cancellationToken);

        var enriched = paged.Items.EnrichUrls(fileUrlProvider, currentUserId);

        LogTaskCommentsFetched(logger, enriched.Count, request.TaskId);

        return new PagedResult<TaskCommentDto>(enriched, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskComments, Level = LogLevel.Information, Message = "Fetching comments for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskComments(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} comments for task ID: {TaskId}")]
    private static partial void LogTaskCommentsFetched(ILogger logger, int count, Guid taskId);
}
