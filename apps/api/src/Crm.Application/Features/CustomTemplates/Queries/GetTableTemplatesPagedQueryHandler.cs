using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTemplates;
using Crm.Application.Dtos.CustomTemplates.Queries;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTemplates.Queries;

/// <summary>
/// Handles the <see cref="GetTableTemplatesPagedQuery"/> to retrieve a paginated list of table templates.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing table template data.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetTableTemplatesPagedQueryHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<GetTableTemplatesPagedQueryHandler> logger) : IRequestHandler<GetTableTemplatesPagedQuery, PagedResult<TableTemplateDto>>
{
    public async Task<PagedResult<TableTemplateDto>> Handle(GetTableTemplatesPagedQuery request, CancellationToken cancellationToken)
    {
        LogFetchingTableTemplates(logger, request.OnlySystemPresets, request.SearchTerm != null, request.PageNumber, request.PageSize);

        var result = await tableTemplateRepository.GetPagedAsync<TableTemplateDto>(
            onlySystemPresets: request.OnlySystemPresets,
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        LogTableTemplatesFetched(logger, result.Items.Count, result.TotalCount);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTableTemplates, Level = LogLevel.Information, Message = "Fetching paginated table templates. OnlySystemPresets: {OnlySystemPresets}, HasSearchFilter: {HasSearchFilter}, Page: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingTableTemplates(ILogger logger, bool? onlySystemPresets, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesFetched, Level = LogLevel.Information, Message = "Successfully fetched {FetchedCount} table template(s) out of {TotalCount} total.")]
    private static partial void LogTableTemplatesFetched(ILogger logger, int fetchedCount, int totalCount);
}
