using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="CreateCustomRecordCommand"/> to lock the parent custom table,
/// validate the payload against the latest table schema, compute the target fractional position,
/// and insert the new custom record atomically.
/// </summary>
/// <param name="customTableRepository">The repository used for locking tables and retrieving custom table field definitions.</param>
/// <param name="customRecordRepository">The repository used for persisting newly created custom records.</param>
/// <param name="customRecordPositionService">The shared service managing fractional-index record positioning.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class CreateCustomRecordCommandHandler(
    ICustomTableRepository customTableRepository,
    ICustomRecordRepository customRecordRepository,
    ICustomRecordPositionService customRecordPositionService,
    ILogger<CreateCustomRecordCommandHandler> logger) : ICommandHandler<CreateCustomRecordCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogCreatingCustomRecord(logger, command.CustomTableId);

        await customTableRepository.LockForUpdateAsync(command.CustomTableId, cancellationToken);

        var customFields = await customTableRepository.GetFieldsByCustomTableIdAsync(command.CustomTableId, cancellationToken);

        if (customFields.Count == 0)
        {
            await customTableRepository.EnsureExistsAsync(command.CustomTableId, cancellationToken);
        }

        var alignedData = command.Data.ValidateAndBackfillPayload(customFields);

        string position = await customRecordPositionService.ComputeInsertPositionAsync(
            command.CustomTableId,
            command.PreviousRecordId,
            command.NextRecordId,
            cancellationToken);

        var record = new CustomRecord
        {
            CustomTableId = command.CustomTableId,
            Position = position,
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
