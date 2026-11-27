using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of users in the organization.
/// Implements the Mediator query pattern returning a paginated collection of user data transfer objects.
/// </summary>
/// <param name="SearchTerm">
/// An optional term to filter users by email, core profile fields, or visible custom field values.
/// If null or whitespace, no text filtering is applied.
/// </param>
/// <param name="OrderBy">
/// The name of the entity property, alias (e.g., "FirstName", "LastName", "FullName", "Email"),
/// or custom field key (e.g., "city", "customFields.age", "customFields['birth_date']") by which the collection should be sorted.
/// Default is "FirstName".
/// </param>
/// <param name="SortOrder">
/// The direction of sorting. Use <see cref="SortOrderConstants.Ascending"/>
/// or <see cref="SortOrderConstants.Descending"/>. Default is <see cref="SortOrderConstants.Ascending"/>.
/// </param>
/// <param name="PageNumber">
/// The target page number to retrieve (1-based index). Default is 1.
/// </param>
/// <param name="PageSize">
/// The maximum number of items to include per page. Default is 10.
/// </param>
/// <param name="ShowDeleted">
/// A value indicating whether to include deactivated (soft-deleted) users in the results. Default is false.
/// </param>
public record GetUsersQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(User.FirstName),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize,
    bool ShowDeleted = false) : IQuery<PagedResult<UserDto>>, IPagedSearchQuery, ICurrentUserAware, ICurrentUserRolesAware
{
    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public List<Guid> CurrentUserRoleIds { get; set; } = [];
}
