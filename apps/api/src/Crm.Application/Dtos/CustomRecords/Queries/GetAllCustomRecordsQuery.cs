using Crm.Application.Common.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using MediatR;

namespace Crm.Application.Dtos.CustomRecords.Queries;

/// <summary>
/// Represents a query to retrieve a paginated list of records for a specific custom table.
/// </summary>
/// <param name="CustomTableId">The unique identifier of the parent custom table.</param>
/// <param name="SearchTerm">An optional search keyword to filter records.</param>
/// <param name="OrderBy">The field to sort by.</param>
/// <param name="SortOrder">The sorting direction (<see cref="SortOrderConstants.Ascending"/> or <see cref="SortOrderConstants.Descending"/>).</param>
/// <param name="PageNumber">The page index number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <returns>A paginated result containing custom record DTOs.</returns>
public record GetAllCustomRecordsQuery(
    Guid CustomTableId,
    string? SearchTerm = null,
    string? OrderBy = nameof(CustomRecordDto.Id),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<CustomRecordDto>>, IPagedSearchQuery;
