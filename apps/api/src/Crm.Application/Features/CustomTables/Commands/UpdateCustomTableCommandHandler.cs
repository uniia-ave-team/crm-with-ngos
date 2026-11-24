using Crm.Application.Common.Consts;
using Crm.Application.Features.CustomTables.Extensions;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles <see cref="UpdateCustomTableCommand"/> to synchronize custom table schemas and record payloads.
/// </summary>
/// <param name="customTableRepository">The repository used to fetch and update custom table metadata and fields.</param>
/// <param name="customRecordRepository">The repository used to verify existing record state.</param>
/// <param name="logger">The logger instance for tracking execution.</param>
public sealed partial class UpdateCustomTableCommandHandler(
    ICustomTableRepository customTableRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<UpdateCustomTableCommandHandler> logger) : ICommandHandler<UpdateCustomTableCommand>
{
    /// <summary>
    /// Handles the command request to update a custom table and apply schema changes.
    /// </summary>
    /// <param name="command">The update custom table command request payload.</param>
    /// <param name="cancellationToken">Cancellation token for cancelling the async execution.</param>
    /// <returns>A task representing the completion of the command handler.</returns>
    /// <exception cref="EntityNotFoundException{CustomTable}">Thrown when table is not found by identifier.</exception>
    /// <exception cref="FieldDataTypeChangeNotAllowedException">Thrown when field data type is modified while records exist.</exception>
    public async ValueTask<Unit> Handle(UpdateCustomTableCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingCustomTable(logger, command.Id, command.Name);

        await customTableRepository.LockForUpdateAsync(command.Id, cancellationToken);
        var customTable = await customTableRepository.GetForUpdateWithFieldsAsync(command.Id, cancellationToken);

        command.Adapt(customTable);

        var hasRecords = await customRecordRepository.AnyAsync(customTable.Id, cancellationToken);
        var diff = customTable.ComputeSchemaDiff(command.Fields, hasRecords);

        customTable.Fields.EnsureValidFieldSchema();

        await customTableRepository.ResolveFieldKeyCollisionsAsync(customTable.Id, cancellationToken);

        if (hasRecords)
        {
            await customRecordRepository.SyncRecordSchemaAsync(customTable.Id, diff, cancellationToken);
        }

        LogCustomTableUpdatedSuccessfully(logger, customTable.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingCustomTable, Level = LogLevel.Information, Message = "Initiating update for custom table ID: {TableId} with Name: {TableName}")]
    private static partial void LogUpdatingCustomTable(ILogger logger, Guid tableId, string tableName);

    [LoggerMessage(EventId = LogEventIds.CustomTableUpdatedSuccessfully, Level = LogLevel.Information, Message = "Custom table and records successfully updated for table ID: {TableId}")]
    private static partial void LogCustomTableUpdatedSuccessfully(ILogger logger, Guid tableId);
}
