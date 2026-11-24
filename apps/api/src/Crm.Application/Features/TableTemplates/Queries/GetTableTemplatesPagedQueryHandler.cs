using Crm.Application.Common.Consts;
using Crm.Application.Dtos.TableTemplates;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.TableTemplates.Queries;

/// <summary>
/// Handles the <see cref="GetTableTemplatesPagedQuery"/> to retrieve a paginated list of table templates.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing table template data.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public sealed partial class GetTableTemplatesPagedQueryHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<GetTableTemplatesPagedQueryHandler> logger) : IQueryHandler<GetTableTemplatesPagedQuery, PagedResult<TableTemplateDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<TableTemplateDto>> Handle(GetTableTemplatesPagedQuery query, CancellationToken cancellationToken)
    {
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(query.SearchTerm);
        LogFetchingTableTemplates(logger, query.OnlySystemPresets, hasSearchFilter, query.PageNumber, query.PageSize);

        var result = await tableTemplateRepository.GetPagedAsync<TableTemplateDto>(
            onlySystemPresets: query.OnlySystemPresets,
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogTableTemplatesFetched(logger, result.Items.Count, result.TotalCount);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingTableTemplates, Level = LogLevel.Information, Message = "Fetching paginated table templates. OnlySystemPresets: {OnlySystemPresets}, HasSearchFilter: {HasSearchFilter}, Page: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingTableTemplates(ILogger logger, bool? onlySystemPresets, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.TableTemplatesFetched, Level = LogLevel.Information, Message = "Successfully fetched {FetchedCount} table template(s) out of {TotalCount} total.")]
    private static partial void LogTableTemplatesFetched(ILogger logger, int fetchedCount, int totalCount);
}
