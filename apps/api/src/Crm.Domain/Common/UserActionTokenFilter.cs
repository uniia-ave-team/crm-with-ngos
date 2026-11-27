using Crm.Domain.Constants;
using Crm.Domain.Entities;

namespace Crm.Domain.Common;

/// <summary>
/// Encapsulates filtering, sorting, and pagination parameters for querying active <see cref="UserActionToken"/> records.
/// </summary>
/// <param name="SearchTerm">Optional case-insensitive search term matching target email or target user name.</param>
/// <param name="OrderBy">Optional sort field name (defaults to <see cref="UserActionToken.CreatedAt"/>).</param>
/// <param name="SortOrder">Sort direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
/// <param name="PageNumber">The 1-based page number to retrieve.</param>
/// <param name="PageSize">The maximum number of items per page.</param>
/// <param name="TargetUserId">Optional filter by targeted user identifier.</param>
/// <param name="CreatedBy">Optional filter by issuing administrator identifier.</param>
public sealed record UserActionTokenFilter(
    string? SearchTerm = null,
    string? OrderBy = nameof(UserActionToken.CreatedAt),
    string? SortOrder = SortOrderConstants.Descending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize,
    Guid? TargetUserId = null,
    Guid? CreatedBy = null);
