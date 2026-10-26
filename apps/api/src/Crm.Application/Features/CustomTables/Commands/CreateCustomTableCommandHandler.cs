using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles the <see cref="CreateCustomTableCommand"/> to create a new custom table from scratch.
/// </summary>
/// <param name="ngoRepository">The repository used for accessing NGO data.</param>
/// <param name="customTableRepository">The repository used for accessing and persisting custom table data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class CreateCustomTableCommandHandler(
    INgoRepository ngoRepository,
    ICustomTableRepository customTableRepository,
    ILogger<CreateCustomTableCommandHandler> logger) : IRequestHandler<CreateCustomTableCommand, Guid>
{
    public async Task<Guid> Handle(CreateCustomTableCommand request, CancellationToken cancellationToken)
    {
        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        LogCreatingCustomTable(logger, request.Name, ngoId);

        var table = request.Adapt<CustomTable>();

        table.NgoId = ngoId;

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
