using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Tasks;
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
/// <param name="fileUrlProvider">The file URL provider for resolving comment author avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetTaskCommentsQueryHandler(
    ITaskCommentRepository commentRepository,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskCommentsQueryHandler> logger) : IQueryHandler<GetTaskCommentsQuery, PagedResult<TaskCommentDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TaskCommentDto>> Handle(GetTaskCommentsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTaskComments(logger, query.TaskId, query.ProjectId);

        var paged = await commentRepository.GetPagedByTaskIdAsync<TaskCommentDto>(
            query.TaskId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var enriched = paged.Items.EnrichUrls(fileUrlProvider, query.CurrentUserId);

        LogTaskCommentsFetched(logger, enriched.Count, query.TaskId);

        return new PagedResult<TaskCommentDto>(enriched, paged.TotalCount, paged.PageNumber, paged.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskComments, Level = LogLevel.Information, Message = "Fetching comments for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskComments(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} comments for task ID: {TaskId}")]
    private static partial void LogTaskCommentsFetched(ILogger logger, int count, Guid taskId);
}
