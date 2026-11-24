using Crm.Application.Common.Consts;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Handles the <see cref="CreateTableTemplateCommand"/> to create a new reusable table template blueprint.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class CreateTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<CreateTableTemplateCommandHandler> logger) : ICommandHandler<CreateTableTemplateCommand, Guid>
{
    /// <inheritdoc />
    public async ValueTask<Guid> Handle(CreateTableTemplateCommand command, CancellationToken cancellationToken)
    {
        LogCreatingTableTemplate(logger, command.Name);

        var template = command.Adapt<TableTemplate>();

        template.Fields.EnsureValidFieldSchema();

        await tableTemplateRepository.CreateAsync(template, cancellationToken);

        LogTableTemplateCreatedSuccessfully(logger, template.Id, template.Fields.Count);

        return template.Id;
    }

    [LoggerMessage(EventId = LogEventIds.CreatingTableTemplate, Level = LogLevel.Information, Message = "Initiating creation of table template: {TemplateName}")]
    private static partial void LogCreatingTableTemplate(ILogger logger, string templateName);

    [LoggerMessage(EventId = LogEventIds.TableTemplateCreatedSuccessfully, Level = LogLevel.Information, Message = "Table template successfully created with ID: {TemplateId} containing {FieldCount} field(s).")]
    private static partial void LogTableTemplateCreatedSuccessfully(ILogger logger, Guid templateId, int fieldCount);
}
