using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Handles the <see cref="GetProjectTagsQuery"/> to retrieve a paginated, filtered, and sorted list of project-scoped tags.
/// </summary>
/// <param name="tagRepository">The repository for querying project-scoped tags.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetProjectTagsQueryHandler(
    ITagRepository tagRepository,
    ILogger<GetProjectTagsQueryHandler> logger) : IQueryHandler<GetProjectTagsQuery, PagedResult<TagDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TagDto>> Handle(GetProjectTagsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingTags(logger, query.ProjectId, query.PageNumber, query.PageSize);

        var result = await tagRepository.GetPagedAsync<TagDto>(
            projectId: query.ProjectId,
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogTagsFetched(logger, result.Items.Count, result.TotalCount, query.ProjectId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTags, Level = LogLevel.Information, Message = "Fetching tags for project ID: {ProjectId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingTags(ILogger logger, Guid projectId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TagsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} tags for project ID: {ProjectId}")]
    private static partial void LogTagsFetched(ILogger logger, int count, int totalCount, Guid projectId);
}
