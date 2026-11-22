using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords;
using Crm.Domain.Common;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Queries;

/// <summary>
/// Handles the <see cref="GetCustomRecordsPagedQuery"/> to retrieve a paginated, filtered, and sorted list of custom records for a table.
/// </summary>
/// <param name="customTableRepository">The repository used for retrieving custom table definitions.</param>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="customRecordRepository">The repository used for retrieving paginated custom record entities.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class GetCustomRecordsPagedQueryHandler(
    ICustomTableRepository customTableRepository,
    ICustomFieldRepository customFieldRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<GetCustomRecordsPagedQueryHandler> logger) : IQueryHandler<GetCustomRecordsPagedQuery, PagedResult<CustomRecordDto>>
{
    /// <inheritdoc />
    public async ValueTask<PagedResult<CustomRecordDto>> Handle(GetCustomRecordsPagedQuery query, CancellationToken cancellationToken)
    {
        bool hasSearchFilter = !string.IsNullOrWhiteSpace(query.SearchTerm);
        LogFetchingCustomRecords(logger, query.CustomTableId, hasSearchFilter, query.PageNumber, query.PageSize);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(query.CustomTableId, cancellationToken);

        if (customFields.Count == 0)
        {
            await customTableRepository.EnsureExistsAsync(query.CustomTableId, cancellationToken);
        }

        var (resolvedOrderBy, fieldDataType) = customFields.ResolveOrdering(query.OrderBy);

        var pagedRecords = await customRecordRepository.GetPagedAsync<CustomRecordDto>(
            customTableId: query.CustomTableId,
            searchTerm: query.SearchTerm,
            orderBy: resolvedOrderBy,
            fieldDataType: fieldDataType,
            sortOrder: query.SortOrder,
            pageNumber: query.PageNumber,
            pageSize: query.PageSize,
            cancellationToken: cancellationToken);

        if (pagedRecords.Items.Count == 0)
        {
            LogCustomRecordsFetched(logger, 0, query.PageNumber, query.CustomTableId);
            return pagedRecords;
        }

        string[] orderedKeys = [.. customFields.OrderBy(x => x.Order).Select(f => f.Key)];

        var reorderedItems = new List<CustomRecordDto>(pagedRecords.Items.Count);
        foreach (var r in pagedRecords.Items)
        {
            reorderedItems.Add(r with { Data = r.Data.ReorderKeys(orderedKeys) });
        }

        LogCustomRecordsFetched(logger, reorderedItems.Count, pagedRecords.PageNumber, query.CustomTableId);

        return new PagedResult<CustomRecordDto>(
            reorderedItems,
            pagedRecords.TotalCount,
            pagedRecords.PageNumber,
            pagedRecords.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomRecords, Level = LogLevel.Information, Message = "Fetching custom records for table ID: {TableId}. HasSearchFilter: {HasSearchFilter}, PageNumber: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingCustomRecords(ILogger logger, Guid tableId, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.CustomRecordsFetched, Level = LogLevel.Information, Message = "Successfully fetched {FetchedCount} custom record(s) on page {PageNumber} for table ID: {TableId}")]
    private static partial void LogCustomRecordsFetched(ILogger logger, int fetchedCount, int pageNumber, Guid tableId);
}
