using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="UpdateCustomRecordCommand"/> to acquire the parent table write lock,
/// validate the payload against the latest table schema, and update an existing custom record's JSON payload.
/// </summary>
/// <param name="customRecordRepository">The repository used for fetching and updating custom record entities.</param>
/// <param name="customTableRepository">The repository used for locking tables and retrieving custom table field definitions.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class UpdateCustomRecordCommandHandler(
    ICustomRecordRepository customRecordRepository,
    ICustomTableRepository customTableRepository,
    ILogger<UpdateCustomRecordCommandHandler> logger) : ICommandHandler<UpdateCustomRecordCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingCustomRecord(logger, command.Id);

        var recordInfo = await customRecordRepository.GetPositionInfoAsync(command.Id, cancellationToken)
            ?? throw new EntityNotFoundException<CustomRecord>(command.Id);

        await customTableRepository.LockForUpdateAsync(recordInfo.CustomTableId, cancellationToken);

        var record = await customRecordRepository.GetForUpdateAsync(command.Id, cancellationToken);

        var customFields = await customTableRepository.GetFieldsByCustomTableIdAsync(record.CustomTableId, cancellationToken);

        var alignedData = command.Data.ValidateAndBackfillPayload(customFields);

        record.Data = alignedData;

        LogCustomRecordUpdatedSuccessfully(logger, record.Id, record.CustomTableId);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingCustomRecord, Level = LogLevel.Information, Message = "Initiating update for custom record ID: {RecordId}")]
    private static partial void LogUpdatingCustomRecord(ILogger logger, Guid recordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordUpdatedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully updated for table ID: {TableId}")]
    private static partial void LogCustomRecordUpdatedSuccessfully(ILogger logger, Guid recordId, Guid tableId);
}
