using Crm.Application.Common.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using MediatR;

namespace Crm.Application.Dtos.CustomTables.Queries;

/// <summary>
/// Represents a query to retrieve a paginated list of all custom tables belonging to an NGO.
/// </summary>
/// <param name="SearchTerm">The term used to filter custom tables by name or description.</param>
/// <param name="OrderBy">The field by which to order the results. Defaults to <see cref="CustomTableSummaryDto.Name"/>.</param>
/// <param name="SortOrder">The direction of sorting (e.g., Ascending or Descending). Defaults to <see cref="SortOrderConstants.Ascending"/>.</param>
/// <param name="PageNumber">The page index number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <returns>A paginated result containing simplified custom table summary DTOs.</returns>
public record GetCustomTablesPagedQuery(
    string? SearchTerm = null,
    string? OrderBy = nameof(CustomTableSummaryDto.Name),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<CustomTableSummaryDto>>, IPagedSearchQuery;
