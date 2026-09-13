using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using MediatR;

namespace Crm.Application.Dtos.Role.Queries;

/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of user roles.
/// Implements the MediatR query pattern returning a paginated collection of role data transfer objects.
/// </summary>
/// <param name="SearchTerm">
/// An optional search term to filter roles by their name or description.
/// If null or whitespace, no text filtering is applied.
/// </param>
/// <param name="OrderBy">
/// The name of the entity property by which the collection should be sorted (e.g., "Name").
/// </param>
/// <param name="SortOrder">
/// The direction of sorting. Use <see cref="SortOrderConstants.Ascending"/> ("asc")
/// or <see cref="SortOrderConstants.Descending"/> ("desc"). Default is ascending.
/// </param>
/// <param name="PageNumber">
/// The target page number to retrieve (1-based index). Default is 1.
/// </param>
/// <param name="PageSize">
/// The maximum number of items to include per page. Default is 10.
/// </param>
public record GetAllRolesQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(AuthRole.Name),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = 1,
    int PageSize = 10) : IRequest<PagedResult<RoleDto>>;
