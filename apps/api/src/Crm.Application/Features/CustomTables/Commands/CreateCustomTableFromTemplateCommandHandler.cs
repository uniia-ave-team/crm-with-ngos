using Crm.Application.Common.Consts;
using Crm.Application.Dtos.TableTemplates;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTables.Commands;

/// <summary>
/// Handles the <see cref="CreateCustomTableFromTemplateCommand"/> to instantiate a new custom table from an existing template preset.
/// </summary>
/// <param name="customTableRepository">The repository used for accessing and persisting custom table data.</param>
/// <param name="tableTemplateRepository">The repository used for retrieving the source table template.</param>
/// <param name="ngoRepository">The repository used for validating the existence of the NGO.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class CreateCustomTableFromTemplateCommandHandler(
    ICustomTableRepository customTableRepository,
    ITableTemplateRepository tableTemplateRepository,
    INgoRepository ngoRepository,
    ILogger<CreateCustomTableFromTemplateCommandHandler> logger) : ICommandHandler<CreateCustomTableFromTemplateCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateCustomTableFromTemplateCommand command, CancellationToken cancellationToken)
    {
        var ngoId = await ngoRepository.GetIdAsync(cancellationToken);

        LogCreatingCustomTableFromTemplate(logger, command.Name, command.SourceTemplateId, ngoId);

        var template = await tableTemplateRepository.GetAsync<TableTemplateDto>(command.SourceTemplateId, cancellationToken);

        var customTable = template.Adapt<CustomTable>();

        customTable.Name = command.Name;
        customTable.NgoId = ngoId;

        if (command.Description is not null)
        {
            customTable.Description = string.IsNullOrWhiteSpace(command.Description)
                ? null
                : command.Description.Trim();
        }

        customTable.Fields.EnsureValidFieldSchema();

        await customTableRepository.CreateAsync(customTable, cancellationToken);

        LogCustomTableInstantiatedSuccessfully(logger, customTable.Id, command.SourceTemplateId, customTable.Fields.Count);

        return customTable.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingCustomTableFromTemplate, Level = LogLevel.Information, Message = "Initiating creation of custom table '{TableName}' from template ID: {TemplateId} for NGO ID: {NgoId}")]
    private static partial void LogCreatingCustomTableFromTemplate(ILogger logger, string tableName, Guid templateId, Guid ngoId);

    [LoggerMessage(EventId = LogEventIds.CustomTableFromTemplateCreatedSuccessfully, Level = LogLevel.Information, Message = "Custom table successfully created with ID: {TableId} from template ID: {TemplateId} containing {FieldCount} field(s).")]
    private static partial void LogCustomTableInstantiatedSuccessfully(ILogger logger, Guid tableId, Guid templateId, int fieldCount);
}
