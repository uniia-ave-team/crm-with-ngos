using Crm.Application.Common.Consts;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="UpdateCustomRecordCommand"/> to validate the payload against the table schema,
/// backfill default values for missing required fields, and update an existing custom record's JSON payload in the database.
/// </summary>
/// <param name="customRecordRepository">The repository used for fetching and updating custom record entities.</param>
/// <param name="customFieldRepository">The repository used for retrieving custom field definitions.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class UpdateCustomRecordCommandHandler(
    ICustomRecordRepository customRecordRepository,
    ICustomFieldRepository customFieldRepository,
    ILogger<UpdateCustomRecordCommandHandler> logger) : ICommandHandler<UpdateCustomRecordCommand>
{
    /// <summary>
    /// Handles the execution of <see cref="UpdateCustomRecordCommand"/> to validate, backfill, and update the custom record payload.
    /// </summary>
    /// <param name="command">The command instance containing record ID and updated data payload.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async ValueTask<Unit> Handle(UpdateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingCustomRecord(logger, command.Id);

        var record = await customRecordRepository.GetForUpdateAsync(command.Id, cancellationToken);

        var customFields = await customFieldRepository.GetByCustomTableIdAsync(record.CustomTableId, cancellationToken);

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
