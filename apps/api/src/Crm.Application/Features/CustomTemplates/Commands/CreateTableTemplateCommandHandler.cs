using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Domain.Entities;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTemplates.Commands;

/// <summary>
/// Handles the <see cref="CreateTableTemplateCommand"/> to create a new reusable table template blueprint.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class CreateTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<CreateTableTemplateCommandHandler> logger) : IRequestHandler<CreateTableTemplateCommand, Guid>
{
    public async Task<Guid> Handle(CreateTableTemplateCommand request, CancellationToken cancellationToken)
    {
        LogCreatingTableTemplate(logger, request.Name);

        var template = request.Adapt<TableTemplate>();

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
