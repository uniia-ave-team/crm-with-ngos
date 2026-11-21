using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Task.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of tasks either for a specific project
/// or globally across all visible projects when <see cref="ProjectId"/> is <c>null</c>
/// (supports personal task filtering <c>filter=mine</c>, tag/status/priority/date filters,
/// text search, and null-safe date sorting).
/// </summary>
/// <param name="Filter">An optional view filter (e.g., <c>mine</c> to return only tasks assigned to the current user).</param>
/// <param name="TopicId">An optional board topic/section identifier filter.</param>
/// <param name="Status">An optional task execution status filter.</param>
/// <param name="Priority">An optional task priority level filter.</param>
/// <param name="TagIds">An optional collection of tag identifiers to match against task tags.</param>
/// <param name="StartDateFrom">An optional lower bound (inclusive) for the task start date.</param>
/// <param name="StartDateTo">An optional upper bound (inclusive) for the task start date.</param>
/// <param name="EndDateFrom">An optional lower bound (inclusive) for the task end date.</param>
/// <param name="EndDateTo">An optional upper bound (inclusive) for the task end date.</param>
/// <param name="SearchTerm">An optional search term matched against task title and description.</param>
/// <param name="OrderBy">An optional property name to sort the tasks by.</param>
/// <param name="SortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
/// <param name="PageNumber">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
public record GetProjectTasksQuery(
    string? Filter = null,
    Guid? TopicId = null,
    ProjectTaskStatus? Status = null,
    TaskPriority? Priority = null,
    IReadOnlyList<Guid>? TagIds = null,
    DateTime? StartDateFrom = null,
    DateTime? StartDateTo = null,
    DateTime? EndDateFrom = null,
    DateTime? EndDateTo = null,
    string? SearchTerm = null,
    string? OrderBy = null,
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<TaskSummaryDto>>, IPagedSearchQuery, IProjectScoped
{
    /// <summary>
    /// Gets the optional unique identifier of the project, populated from the route parameter or <c>null</c> for global task queries.
    /// </summary>
    [JsonIgnore]
    public Guid? ProjectId { get; init; }

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
