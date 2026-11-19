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
/// Handles the <see cref="GetTaskCommentsQuery"/> to retrieve all comments on a task.
/// </summary>
/// <param name="commentRepository">The repository for querying task comments.</param>
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="fileUrlProvider">The file URL provider for resolving comment author avatar URLs.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetTaskCommentsQueryHandler(
    ITaskCommentRepository commentRepository,
    ICurrentUserService currentUserService,
    IFileUrlProvider fileUrlProvider,
    ILogger<GetTaskCommentsQueryHandler> logger) : IRequestHandler<GetTaskCommentsQuery, List<TaskCommentDto>>
{
    /// <inheritdoc />
    public async ValueTask<List<TaskCommentDto>> Handle(GetTaskCommentsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingTaskComments(logger, request.TaskId, request.ProjectId);

        var comments = await commentRepository.GetByTaskIdAsync<TaskCommentDto>(request.TaskId, cancellationToken);
        var result = comments.EnrichUrls(fileUrlProvider, currentUserId);

        LogTaskCommentsFetched(logger, result.Count, request.TaskId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTaskComments, Level = LogLevel.Information, Message = "Fetching comments for task ID: {TaskId} in project ID: {ProjectId}")]
    private static partial void LogFetchingTaskComments(ILogger logger, Guid taskId, Guid projectId);

    [LoggerMessage(EventId = LogEventIds.TaskCommentsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} comments for task ID: {TaskId}")]
    private static partial void LogTaskCommentsFetched(ILogger logger, int count, Guid taskId);
}
