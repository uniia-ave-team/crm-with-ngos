using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Projects;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Mediator;

namespace Crm.Application.Features.Projects.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of projects visible to the currently authenticated user.
/// </summary>
/// <param name="SearchTerm">An optional search term matched against project title and description.</param>
/// <param name="OrderBy">The property name to sort the projects by.</param>
/// <param name="SortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
/// <param name="PageNumber">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
public record GetProjectsQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(ProjectSummaryDto.Title),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IQuery<PagedResult<ProjectSummaryDto>>, IPagedSearchQuery
{
    /// <summary>
    /// Gets the unique identifier of the currently authenticated user, populated by the controller.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; init; }
}
