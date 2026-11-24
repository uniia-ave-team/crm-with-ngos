using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Handles the <see cref="GetProjectsQuery"/> to retrieve a paginated, filtered, and sorted list of projects
/// visible to the currently authenticated user.
/// </summary>
/// <param name="projectRepository">The repository for querying visible projects.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public sealed partial class GetProjectsQueryHandler(
    IProjectRepository projectRepository,
    ILogger<GetProjectsQueryHandler> logger) : IQueryHandler<GetProjectsQuery, PagedResult<ProjectSummaryDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<ProjectSummaryDto>> Handle(GetProjectsQuery query, CancellationToken cancellationToken)
    {
        LogFetchingProjects(logger, query.CurrentUserId, query.PageNumber, query.PageSize);

        var result = await projectRepository.GetPagedAsync<ProjectSummaryDto>(
            userId: query.CurrentUserId,
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogProjectsFetched(logger, result.Items.Count, result.TotalCount, query.CurrentUserId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjects, Level = LogLevel.Information, Message = "Fetching visible projects for user ID: {UserId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingProjects(ILogger logger, Guid userId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.ProjectsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} visible projects for user ID: {UserId}")]
    private static partial void LogProjectsFetched(ILogger logger, int count, int totalCount, Guid userId);
}
