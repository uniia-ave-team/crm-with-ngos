using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="CreateCustomRecordCommand"/> to validate the payload against the table schema,
/// backfill default values for missing required fields, and persist a new custom record in the database.
/// </summary>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="customRecordRepository">The repository used for storing new custom record entities.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class CreateCustomRecordCommandHandler(
    ICustomFieldRepository customFieldRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<CreateCustomRecordCommandHandler> logger) : ICommandHandler<CreateCustomRecordCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogCreatingCustomRecord(logger, command.CustomTableId);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(command.CustomTableId, cancellationToken);

        if (customFields.Count == 0)
        {
            throw new EntityNotFoundException<CustomTable>(command.CustomTableId);
        }

        var alignedData = command.Data.ValidateAndBackfillPayload(customFields);

        var record = new CustomRecord
        {
            CustomTableId = command.CustomTableId,
            Data = alignedData,
        };

        await customRecordRepository.CreateAsync(record, cancellationToken);

        LogCustomRecordCreatedSuccessfully(logger, record.Id, command.CustomTableId);

        return record.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingCustomRecord, Level = LogLevel.Information, Message = "Initiating creation of a new custom record for table ID: {TableId}")]
    private static partial void LogCreatingCustomRecord(ILogger logger, Guid tableId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordCreatedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully created for table ID: {TableId}")]
    private static partial void LogCustomRecordCreatedSuccessfully(ILogger logger, Guid recordId, Guid tableId);
}
