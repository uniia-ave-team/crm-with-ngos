using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Queries;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Handles the <see cref="GetProjectTopicsQuery"/> to retrieve a paginated, filtered, and sorted list of board topics for a project.
/// </summary>
/// <param name="topicRepository">The repository for querying project board topics.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetProjectTopicsQueryHandler(
    ITopicRepository topicRepository,
    ILogger<GetProjectTopicsQueryHandler> logger) : IRequestHandler<GetProjectTopicsQuery, PagedResult<TopicDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TopicDto>> Handle(GetProjectTopicsQuery request, CancellationToken cancellationToken)
    {
        LogFetchingTopics(logger, request.ProjectId, request.PageNumber, request.PageSize);

        var result = await topicRepository.GetPagedAsync<TopicDto>(
            projectId: request.ProjectId,
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        LogTopicsFetched(logger, result.Items.Count, result.TotalCount, request.ProjectId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTopics, Level = LogLevel.Information, Message = "Fetching topics for project ID: {ProjectId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingTopics(ILogger logger, Guid projectId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TopicsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} topics for project ID: {ProjectId}")]
    private static partial void LogTopicsFetched(ILogger logger, int count, int totalCount, Guid projectId);
}
