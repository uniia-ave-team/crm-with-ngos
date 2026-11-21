using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Queries;
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
public partial class GetProjectTagsQueryHandler(
    ITagRepository tagRepository,
    ILogger<GetProjectTagsQueryHandler> logger) : IRequestHandler<GetProjectTagsQuery, PagedResult<TagDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TagDto>> Handle(GetProjectTagsQuery request, CancellationToken cancellationToken)
    {
        LogFetchingTags(logger, request.ProjectId, request.PageNumber, request.PageSize);

        var result = await tagRepository.GetPagedAsync<TagDto>(
            projectId: request.ProjectId,
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        LogTagsFetched(logger, result.Items.Count, result.TotalCount, request.ProjectId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTags, Level = LogLevel.Information, Message = "Fetching tags for project ID: {ProjectId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingTags(ILogger logger, Guid projectId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TagsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} tags for project ID: {ProjectId}")]
    private static partial void LogTagsFetched(ILogger logger, int count, int totalCount, Guid projectId);
}
