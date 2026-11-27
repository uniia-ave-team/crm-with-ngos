using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.Users;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Crm.Domain.Entities;
using Mediator;

namespace Crm.Application.Features.Users.Queries;

/// <summary>
/// Query to retrieve a paginated, filtered, and sorted list of active (unexpired) user invitation tokens.
/// </summary>
/// <param name="SearchTerm">Optional case-insensitive search term filtering by invited email address.</param>
/// <param name="OrderBy">Optional sort field (<c>Email</c>, <c>CreatedAt</c>, or <c>ExpiresAt</c>). Defaults to <c>CreatedAt</c>.</param>
/// <param name="SortOrder">Sort direction (<c>asc</c> or <c>desc</c>). Defaults to <c>desc</c>.</param>
/// <param name="PageNumber">The 1-based page number to retrieve. Defaults to 1.</param>
/// <param name="PageSize">The maximum number of items per page. Defaults to 10.</param>
/// <param name="CreatedBy">Optional filter by the issuing administrator's user identifier.</param>
public record GetActiveInvitationsQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(UserActionToken.CreatedAt),
    string? SortOrder = SortOrderConstants.Descending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize,
    Guid? CreatedBy = null) : IQuery<PagedResult<ActiveInvitationDto>>, IPagedSearchQuery
{
    /// <summary>
    /// Converts this query into a domain <see cref="UserActionTokenFilter"/>.
    /// </summary>
    /// <returns>A populated <see cref="UserActionTokenFilter"/> instance.</returns>
    public UserActionTokenFilter ToFilter()
        => new(SearchTerm, OrderBy, SortOrder, PageNumber, PageSize, CreatedBy: CreatedBy);
}
