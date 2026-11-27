using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles the <see cref="CreateCustomTableCommand"/> to create a new custom table from scratch
/// with server-generated immutable field keys.
/// </summary>
/// <param name="ngoRepository">The repository used for accessing NGO data.</param>
/// <param name="customTableRepository">The repository used for accessing and persisting custom table data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class CreateCustomTableCommandHandler(
    INgoRepository ngoRepository,
    ICustomTableRepository customTableRepository,
    ILogger<CreateCustomTableCommandHandler> logger) : ICommandHandler<CreateCustomTableCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateCustomTableCommand command, CancellationToken cancellationToken)
    {
        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        LogCreatingCustomTable(logger, command.Name, ngoId);

        var table = new CustomTable
        {
            NgoId = ngoId,
            Name = command.Name,
            Description = command.Description,
            Fields =
            [
                .. command.Fields.Select(f => new CustomField
                {
                    Name = f.Name,
                    IsRequired = f.IsRequired,
                    Order = f.Order,
                }),
            ],
        };

        table.Fields.EnsureValidFieldSchema();

        await customTableRepository.CreateAsync(table, cancellationToken);

        LogCustomTableCreatedSuccessfully(logger, table.Id, table.Fields.Count);

        return table.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingCustomTable, Level = LogLevel.Information, Message = "Initiating creation of custom table: {TableName} for NGO ID: {NgoId}")]
    private static partial void LogCreatingCustomTable(ILogger logger, string tableName, Guid ngoId);

    [LoggerMessage(EventId = LogEventIds.CustomTableCreatedSuccessfully, Level = LogLevel.Information, Message = "Custom table successfully created with ID: {TableId} containing {FieldCount} field(s).")]
    private static partial void LogCustomTableCreatedSuccessfully(ILogger logger, Guid tableId, int fieldCount);
}
