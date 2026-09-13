using Crm.Domain.Common;
using Crm.Domain.Consts;
using Crm.Domain.Entities;
using MediatR;

namespace Crm.Application.Dtos.User.Queries;
/// <summary>
/// Represents a query to retrieve a paginated, filtered, and sorted list of users in the organization.
/// Implements the MediatR query pattern returning a paginated collection of user data transfer objects.
/// </summary>
/// <param name="SearchTerm">
/// An optional term to filter users by email, first name, or last name.
/// If null or whitespace, no text filtering is applied.
/// </param>
/// <param name="OrderBy">
/// The name of the entity property by which the collection should be sorted (e.g., "Email", "FirstName").
/// Default is "Email".
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
/// <param name="ShowDeleted">
/// A value indicating whether to include deactivated (soft-deleted) users in the results. Default is false.
/// </param>
public record GetUsersQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(AuthUser.Email),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = 1,
    int PageSize = 10,
    bool ShowDeleted = false) : IRequest<PagedResult<UserDto>>;
