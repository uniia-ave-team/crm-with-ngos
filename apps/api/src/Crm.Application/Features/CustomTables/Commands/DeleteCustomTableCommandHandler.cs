using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles the <see cref="DeleteCustomTableCommand"/> to remove a custom table.
/// Cascading deletes configured at the database level automatically delete associated fields and records.
/// </summary>
/// <param name="customTableRepository">The repository used for accessing and deleting custom table data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class DeleteCustomTableCommandHandler(
    ICustomTableRepository customTableRepository,
    ILogger<DeleteCustomTableCommandHandler> logger) : ICommandHandler<DeleteCustomTableCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteCustomTableCommand command, CancellationToken cancellationToken)
    {
        LogDeletingCustomTable(logger, command.Id);

        await customTableRepository.DeleteAsync(command.Id, cancellationToken);

        LogCustomTableDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingCustomTable, Level = LogLevel.Information, Message = "Initiating deletion of custom table ID: {TableId}")]
    private static partial void LogDeletingCustomTable(ILogger logger, Guid tableId);

    [LoggerMessage(EventId = LogEventIds.CustomTableDeletedSuccessfully, Level = LogLevel.Information, Message = "Custom table successfully deleted with ID: {TableId}")]
    private static partial void LogCustomTableDeletedSuccessfully(ILogger logger, Guid tableId);
}
