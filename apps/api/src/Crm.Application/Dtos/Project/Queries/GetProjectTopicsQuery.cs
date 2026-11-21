using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Dtos.Project.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of board topics/sections for a project.
/// </summary>
/// <param name="SearchTerm">An optional search term matched against topic title.</param>
/// <param name="OrderBy">The property name to sort the topics by.</param>
/// <param name="SortOrder">The sort direction (<c>asc</c> or <c>desc</c>).</param>
/// <param name="PageNumber">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
public record GetProjectTopicsQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(TopicDto.Order),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<TopicDto>>, IPagedSearchQuery, IProjectScoped
{
    /// <summary>
    /// Gets the unique identifier of the project, populated from the route parameter.
    /// </summary>
    [JsonIgnore]
    public Guid ProjectId { get; init; }

    /// <inheritdoc />
    Guid? IProjectScoped.ProjectId => ProjectId;

    /// <inheritdoc />
    ProjectClaim IProjectScoped.RequiredProjectClaim => ProjectClaim.View;
}
