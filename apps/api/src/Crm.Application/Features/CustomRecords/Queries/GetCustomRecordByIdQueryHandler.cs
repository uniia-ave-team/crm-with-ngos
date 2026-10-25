using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords;
using Crm.Application.Dtos.CustomRecords.Queries;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Queries;

/// <summary>
/// Handles the <see cref="GetCustomRecordByIdQuery"/> to retrieve a single custom record DTO by its unique identifier.
/// </summary>
/// <param name="customRecordRepository">The repository used for fetching custom record entities.</param>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public partial class GetCustomRecordByIdQueryHandler(
    ICustomRecordRepository customRecordRepository,
    ICustomFieldRepository customFieldRepository,
    ILogger<GetCustomRecordByIdQueryHandler> logger) : IRequestHandler<GetCustomRecordByIdQuery, CustomRecordDto>
{
    public async Task<CustomRecordDto> Handle(GetCustomRecordByIdQuery request, CancellationToken cancellationToken)
    {
        LogFetchingCustomRecordById(logger, request.Id);

        var record = await customRecordRepository.GetAsync(request.Id, cancellationToken);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(record.CustomTableId, cancellationToken);

        LogCustomRecordByIdFetched(logger, record.Id);

        return record.Adapt<CustomRecordDto>() with
        {
            Data = record.Data.ReorderKeys([.. customFields.OrderBy(x => x.Order).Select(f => f.Key)]),
        };
    }

    [LoggerMessage(EventId = LogEventIds.FetchingCustomRecordById, Level = LogLevel.Information, Message = "Fetching custom record by ID: {RecordId}")]
    private static partial void LogFetchingCustomRecordById(ILogger logger, Guid recordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordByIdFetched, Level = LogLevel.Information, Message = "Successfully fetched custom record ID: {RecordId}")]
    private static partial void LogCustomRecordByIdFetched(ILogger logger, Guid recordId);
}
