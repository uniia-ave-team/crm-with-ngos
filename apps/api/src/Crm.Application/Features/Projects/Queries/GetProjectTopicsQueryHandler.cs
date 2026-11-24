using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
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
public sealed partial class GetProjectTopicsQueryHandler(
    ITopicRepository topicRepository,
    ILogger<GetProjectTopicsQueryHandler> logger) : IQueryHandler<GetProjectTopicsQuery, PagedResult<TopicDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TopicDto>> Handle(GetProjectTopicsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTopics(logger, query.ProjectId, query.PageNumber, query.PageSize);

        var result = await topicRepository.GetPagedAsync<TopicDto>(
            projectId: query.ProjectId,
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogTopicsFetched(logger, result.Items.Count, result.TotalCount, query.ProjectId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTopics, Level = LogLevel.Information, Message = "Fetching topics for project ID: {ProjectId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingTopics(ILogger logger, Guid projectId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TopicsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} topics for project ID: {ProjectId}")]
    private static partial void LogTopicsFetched(ILogger logger, int count, int totalCount, Guid projectId);
}
