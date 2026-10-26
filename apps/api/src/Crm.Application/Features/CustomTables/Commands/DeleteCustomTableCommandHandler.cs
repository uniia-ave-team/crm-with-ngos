using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles the <see cref="DeleteCustomTableCommand"/> to remove a custom table.
/// Cascading deletes configured at the database level automatically delete associated fields and records.
/// </summary>
/// <param name="customTableRepository">The repository used for accessing and deleting custom table data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class DeleteCustomTableCommandHandler(
    ICustomTableRepository customTableRepository,
    ILogger<DeleteCustomTableCommandHandler> logger) : IRequestHandler<DeleteCustomTableCommand>
{
    public async Task Handle(DeleteCustomTableCommand request, CancellationToken cancellationToken)
    {
        LogDeletingCustomTable(logger, request.Id);

        await customTableRepository.DeleteAsync(request.Id, cancellationToken);

        LogCustomTableDeletedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingCustomTable, Level = LogLevel.Information, Message = "Initiating deletion of custom table ID: {TableId}")]
    private static partial void LogDeletingCustomTable(ILogger logger, Guid tableId);

    [LoggerMessage(EventId = LogEventIds.CustomTableDeletedSuccessfully, Level = LogLevel.Information, Message = "Custom table successfully deleted with ID: {TableId}")]
    private static partial void LogCustomTableDeletedSuccessfully(ILogger logger, Guid tableId);
}
