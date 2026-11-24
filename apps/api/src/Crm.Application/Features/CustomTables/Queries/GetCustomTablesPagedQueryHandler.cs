using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTables;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Queries;

/// <summary>
/// Handles the <see cref="GetCustomTablesPagedQuery"/> to retrieve a paginated list of custom tables for a specific NGO.
/// </summary>
/// <param name="customTableRepository">The repository used for accessing custom table data.</param>
/// <param name="ngoRepository">The repository used for validating the existence of the NGO.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public sealed partial class GetCustomTablesPagedQueryHandler(
    ICustomTableRepository customTableRepository,
    INgoRepository ngoRepository,
    ILogger<GetCustomTablesPagedQueryHandler> logger) : IQueryHandler<GetCustomTablesPagedQuery, PagedResult<CustomTableSummaryDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<CustomTableSummaryDto>> Handle(GetCustomTablesPagedQuery query, CancellationToken cancellationToken)
    {
        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        bool hasSearchFilter = !string.IsNullOrWhiteSpace(query.SearchTerm);

        LogFetchingCustomTables(logger, ngoId, hasSearchFilter, query.PageNumber, query.PageSize);

        var result = await customTableRepository.GetPagedAsync<CustomTableSummaryDto>(
            ngoId: ngoId,
            searchTerm: query.SearchTerm,
            orderBy: query.OrderBy,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        LogCustomTablesFetched(logger, result.Items.Count, result.TotalCount);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomTables, Level = LogLevel.Information, Message = "Fetching paginated custom tables for NGO ID: {NgoId}. HasSearchFilter: {HasSearchFilter}, Page: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingCustomTables(ILogger logger, Guid ngoId, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.CustomTablesFetched, Level = LogLevel.Information, Message = "Successfully fetched {FetchedCount} custom table(s) out of {TotalCount} total.")]
    private static partial void LogCustomTablesFetched(ILogger logger, int fetchedCount, int totalCount);
}
