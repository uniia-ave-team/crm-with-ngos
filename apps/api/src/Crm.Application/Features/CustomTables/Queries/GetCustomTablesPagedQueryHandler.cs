using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Dtos.CustomTables.Queries;
using Crm.Domain.Common;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Queries;

/// <summary>
/// Handles the <see cref="GetCustomTablesPagedQuery"/> to retrieve a paginated list of custom tables for a specific NGO.
/// </summary>
/// <param name="customTableRepository">The repository used for accessing custom table data.</param>
/// <param name="ngoRepository">The repository used for validating the existence of the NGO.</param>
/// <param name="logger">The logger instance for tracking query execution.</param>
public partial class GetCustomTablesPagedQueryHandler(
    ICustomTableRepository customTableRepository,
    INgoRepository ngoRepository,
    ILogger<GetCustomTablesPagedQueryHandler> logger) : IRequestHandler<GetCustomTablesPagedQuery, PagedResult<CustomTableSummaryDto>>
{
    public async Task<PagedResult<CustomTableSummaryDto>> Handle(GetCustomTablesPagedQuery request, CancellationToken cancellationToken)
    {
        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        LogFetchingCustomTables(logger, ngoId, request.SearchTerm != null, request.PageNumber, request.PageSize);

        var result = await customTableRepository.GetPagedAsync<CustomTableSummaryDto>(
            ngoId: ngoId,
            searchTerm: request.SearchTerm,
            orderBy: request.OrderBy,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        LogCustomTablesFetched(logger, result.Items.Count, result.TotalCount);

        return result;
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomTables, Level = LogLevel.Information, Message = "Fetching paginated custom tables for NGO ID: {NgoId}. HasSearchFilter: {HasSearchFilter}, Page: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingCustomTables(ILogger logger, Guid ngoId, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.CustomTablesFetched, Level = LogLevel.Information, Message = "Successfully fetched {FetchedCount} custom table(s) out of {TotalCount} total.")]
    private static partial void LogCustomTablesFetched(ILogger logger, int fetchedCount, int totalCount);
}
