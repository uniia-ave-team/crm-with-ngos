using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Queries;

/// <summary>
/// Handles the <see cref="GetCustomRecordByIdQuery"/> to retrieve a single custom record DTO by its unique identifier.
/// </summary>
/// <param name="customRecordRepository">The repository used for fetching custom record entities.</param>
/// <param name="customTableRepository">The repository used for retrieving custom table field definitions.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class GetCustomRecordByIdQueryHandler(
    ICustomRecordRepository customRecordRepository,
    ICustomTableRepository customTableRepository,
    ILogger<GetCustomRecordByIdQueryHandler> logger) : IQueryHandler<GetCustomRecordByIdQuery, CustomRecordDto>
{
    /// <inheritdoc />
    public async ValueTask<CustomRecordDto> Handle(GetCustomRecordByIdQuery query, CancellationToken cancellationToken)
    {
        LogFetchingCustomRecordById(logger, query.Id);

        var record = await customRecordRepository.GetAsync(query.Id, cancellationToken);

        var customFields = await customTableRepository.GetFieldsByCustomTableIdAsync(record.CustomTableId, cancellationToken);

        string[] orderedKeys = [.. customFields.OrderBy(x => x.Order).Select(f => f.Id.ToString())];

        LogCustomRecordByIdFetched(logger, record.Id);

        return new CustomRecordDto(
            record.Id,
            record.Data.ReorderKeys(orderedKeys));
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomRecordById, Level = LogLevel.Information, Message = "Fetching custom record by ID: {RecordId}")]
    private static partial void LogFetchingCustomRecordById(ILogger logger, Guid recordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordByIdFetched, Level = LogLevel.Information, Message = "Successfully fetched custom record ID: {RecordId}")]
    private static partial void LogCustomRecordByIdFetched(ILogger logger, Guid recordId);
}
