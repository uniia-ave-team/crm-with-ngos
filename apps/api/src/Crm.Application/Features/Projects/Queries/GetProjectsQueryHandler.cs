using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Queries;
using Crm.Application.Interfaces;
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
/// <param name="currentUserService">The service for retrieving the currently authenticated user's identifier.</param>
/// <param name="logger">The logger instance for recording handler events.</param>
public partial class GetProjectsQueryHandler(
    IProjectRepository projectRepository,
    ICurrentUserService currentUserService,
    ILogger<GetProjectsQueryHandler> logger) : IRequestHandler<GetProjectsQuery, PagedResult<ProjectSummaryDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<ProjectSummaryDto>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.GetUserId();
        LogFetchingProjects(logger, currentUserId, request.PageNumber, request.PageSize);

        var result = await projectRepository.GetPagedAsync<ProjectSummaryDto>(
            userId: currentUserId,
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        LogProjectsFetched(logger, result.Items.Count, result.TotalCount, currentUserId);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingProjects, Level = LogLevel.Information, Message = "Fetching visible projects for user ID: {UserId} (Page: {PageNumber}, Size: {PageSize})")]
    private static partial void LogFetchingProjects(ILogger logger, Guid userId, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.ProjectsFetched, Level = LogLevel.Information, Message = "Retrieved {Count} out of {TotalCount} visible projects for user ID: {UserId}")]
    private static partial void LogProjectsFetched(ILogger logger, int count, int totalCount, Guid userId);
}
