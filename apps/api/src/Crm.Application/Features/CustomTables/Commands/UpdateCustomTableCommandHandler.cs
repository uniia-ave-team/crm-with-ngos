using Crm.Application.Common.Consts;
using Crm.Application.Extensions;
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
/// <param name="customRecordRepository">The repository used to verify existing records and patch JSONB payloads.</param>
/// <param name="logger">The logger instance for tracking execution.</param>
public sealed partial class UpdateCustomTableCommandHandler(
    ICustomTableRepository customTableRepository,
    ICustomRecordRepository customRecordRepository,
    ILogger<UpdateCustomTableCommandHandler> logger) : ICommandHandler<UpdateCustomTableCommand>
{
    /// <inheritdoc />
    /// <exception cref="EntityNotFoundException{CustomTable}">Thrown when table is not found by identifier.</exception>
    public async ValueTask<Unit> Handle(UpdateCustomTableCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingCustomTable(logger, command.Id, command.Name);

        await customTableRepository.LockForUpdateAsync(command.Id, cancellationToken);
        var customTable = await customTableRepository.GetForUpdateWithFieldsAsync(command.Id, cancellationToken);

        command.Adapt(customTable);

        var plan = customTable.Fields.SynchronizeFields(command.Fields);

        customTable.Fields.EnsureValidFieldSchema();

        await customTableRepository.ApplyFieldSchemaChangesAsync(
            customTable.Id,
            plan.AddedFields,
            plan.RemovedFields,
            cancellationToken);

        if (plan.RequiresJsonbSync && await customRecordRepository.AnyAsync(customTable.Id, cancellationToken))
        {
            await customRecordRepository.SyncRecordSchemaAsync(customTable.Id, plan, cancellationToken);
        }

        LogCustomTableUpdatedSuccessfully(logger, customTable.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingCustomTable, Level = LogLevel.Information, Message = "Initiating update for custom table ID: {TableId} with Name: {TableName}")]
    private static partial void LogUpdatingCustomTable(ILogger logger, Guid tableId, string tableName);

    [LoggerMessage(EventId = LogEventIds.CustomTableUpdatedSuccessfully, Level = LogLevel.Information, Message = "Custom table and records successfully updated for table ID: {TableId}")]
    private static partial void LogCustomTableUpdatedSuccessfully(ILogger logger, Guid tableId);
}
