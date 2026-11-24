using Crm.Application.Common.Interfaces;
using Crm.Application.Dtos.TableTemplates;
using Crm.Domain.Common;
using Crm.Domain.Constants;
using Mediator;

namespace Crm.Application.Features.TableTemplates.Queries;

/// <summary>
/// Represents a query to retrieve a paginated list of table templates available for instantiation.
/// </summary>
/// <param name="OnlySystemPresets">Optional filter to retrieve only system-wide default presets.</param>
/// <param name="SearchTerm">An optional search term to filter the results.</param>
/// <param name="OrderBy">The property to order the results by.</param>
/// <param name="SortOrder">The order in which to sort the results.</param>
/// <param name="PageNumber">The page index number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <returns>A paginated list of template DTOs.</returns>
public record GetTableTemplatesPagedQuery(
    bool? OnlySystemPresets = null,
    string? SearchTerm = null,
    string? OrderBy = nameof(TableTemplateDto.Name),
    string? SortOrder = SortOrderConstants.Ascending,
    int PageNumber = PaginationConstants.MinPageNumber,
    int PageSize = PaginationConstants.DefaultPageSize) : IQuery<PagedResult<TableTemplateDto>>, IPagedSearchQuery;
