using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

// TODO: Make all handlers sealed

/// <summary>
/// Handles the <see cref="CreateCustomRecordCommand"/> to validate, reorder key-value pairs according to table schema,
/// and persist a new custom record in the database.
/// </summary>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="customRecordRepository">The repository used for storing new custom record entities.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public partial class CreateCustomRecordCommandHandler(
    ICustomFieldRepository customFieldRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<CreateCustomRecordCommandHandler> logger) : IRequestHandler<CreateCustomRecordCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomRecordCommand request, CancellationToken cancellationToken)
    {
        LogCreatingCustomRecord(logger, request.CustomTableId);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(request.CustomTableId, cancellationToken);

        if (customFields is null || customFields.Count == 0)
        {
            throw new EntityNotFoundException<CustomTable>(request.CustomTableId);
        }

        var alignedData = request.Data.ValidateAndBackfillPayload(customFields);

        var record = new CustomRecord
        {
            CustomTableId = request.CustomTableId,
            Data = alignedData,
        };

        await customRecordRepository.CreateAsync(record, cancellationToken);

        LogCustomRecordCreatedSuccessfully(logger, record.Id, request.CustomTableId);

        return record.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingCustomRecord, Level = LogLevel.Information, Message = "Initiating creation of a new custom record for table ID: {TableId}")]
    private static partial void LogCreatingCustomRecord(ILogger logger, Guid tableId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordCreatedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully created for table ID: {TableId}")]
    private static partial void LogCustomRecordCreatedSuccessfully(ILogger logger, Guid recordId, Guid tableId);
}
