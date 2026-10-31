using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomRecords.Queries;
using Crm.Domain.Common;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Queries;

/// <summary>
/// Handles the <see cref="GetAllCustomRecordsQuery"/> to retrieve a paginated, filtered, and sorted list of custom records for a table.
/// </summary>
/// <param name="customTableRepository">The repository used for retrieving custom table definitions.</param>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="customRecordRepository">The repository used for retrieving paginated custom record entities.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public partial class GetAllCustomRecordsQueryHandler(
    ICustomTableRepository customTableRepository,
    ICustomFieldRepository customFieldRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<GetAllCustomRecordsQueryHandler> logger) : IRequestHandler<GetAllCustomRecordsQuery, PagedResult<CustomRecordDto>>
{
    public async Task<PagedResult<CustomRecordDto>> Handle(GetAllCustomRecordsQuery request, CancellationToken cancellationToken)
    {
        LogFetchingCustomRecords(logger, request.CustomTableId, request.SearchTerm != null, request.PageNumber, request.PageSize);

        await customTableRepository.EnsureExistsAsync(request.CustomTableId, cancellationToken);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(request.CustomTableId, cancellationToken);

        var (resolvedOrderBy, fieldDataType) = customFields.ResolveOrdering(request.OrderBy);

        var pagedRecords = await customRecordRepository.GetPagedAsync<CustomRecordDto>(
            customTableId: request.CustomTableId,
            searchTerm: request.SearchTerm,
            orderBy: resolvedOrderBy,
            fieldDataType: fieldDataType,
            sortOrder: request.SortOrder,
            pageNumber: request.PageNumber,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        string[] orderedKeys = [.. customFields.OrderBy(x => x.Order).Select(f => f.Key)];

        var reorderedItems = new List<CustomRecordDto>(pagedRecords.Items.Count);
        foreach (var r in pagedRecords.Items)
        {
            reorderedItems.Add(r with { Data = r.Data.ReorderKeys(orderedKeys) });
        }

        LogCustomRecordsFetched(logger, request.CustomTableId, pagedRecords.TotalCount, pagedRecords.PageNumber);

        return new PagedResult<CustomRecordDto>(
                    reorderedItems,
                    pagedRecords.TotalCount,
                    pagedRecords.PageNumber,
                    pagedRecords.PageSize);
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomRecords, Level = LogLevel.Information, Message = "Fetching custom records for table ID: {TableId}. HasSearchFilter: {HasSearchFilter}, PageNumber: {PageNumber}, PageSize: {PageSize}")]
    private static partial void LogFetchingCustomRecords(ILogger logger, Guid tableId, bool hasSearchFilter, int pageNumber, int pageSize);

    [LoggerMessage(EventId = LogEventIds.CustomRecordsFetched, Level = LogLevel.Information, Message = "Successfully fetched {TotalCount} custom record(s) on page {PageNumber} for table ID: {TableId}")]
    private static partial void LogCustomRecordsFetched(ILogger logger, Guid tableId, int totalCount, int pageNumber);
}
