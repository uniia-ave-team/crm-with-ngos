using Crm.Application.Common.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Consts;
using MediatR;

namespace Crm.Application.Dtos.CustomTemplates.Queries;

/// <summary>
/// Represents a query to retrieve a paginated list of table templates available for instantiation.
/// </summary>
/// <param name="OnlySystemPresets">Optional filter to retrieve only system-wide default presets.</param>
/// <param name="PageNumber">The page index number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <returns>A paginated list of template DTOs.</returns>
public record GetTableTemplatesPagedQuery(
    bool? OnlySystemPresets = null,
    string? SearchTerm = null,
    string? OrderBy = nameof(TableTemplateDto.Name),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IRequest<PagedResult<TableTemplateDto>>, IPagedSearchQuery;
