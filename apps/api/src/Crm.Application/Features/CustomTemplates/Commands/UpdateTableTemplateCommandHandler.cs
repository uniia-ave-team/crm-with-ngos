using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Application.Extensions;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTemplates.Commands;

/// <summary>
/// Handles the <see cref="UpdateTableTemplateCommand"/> to update an existing table template blueprint and synchronize its fields.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class UpdateTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<UpdateTableTemplateCommandHandler> logger) : IRequestHandler<UpdateTableTemplateCommand>
{
    public async Task Handle(UpdateTableTemplateCommand request, CancellationToken cancellationToken)
    {
        LogUpdatingTableTemplate(logger, request.Id, request.Name);

        var template = await tableTemplateRepository.GetForUpdateWithFieldsAsync(request.Id, cancellationToken);

        if (template.IsSystemPreset)
        {
            LogCannotModifySystemPreset(logger, template.Id);
            throw new CannotModifySystemPresetException(template.Id);
        }

        request.Adapt(template);

        FieldSchemaSynchronizer.Synchronize(
                    template.Fields,
                    request.Fields,
                    id => new TemplateFieldNotFoundException(id, template.Id));

        template.Fields.EnsureValidFieldSchema();

        LogTableTemplateUpdatedSuccessfully(logger, template.Id);
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTableTemplate, Level = LogLevel.Information, Message = "Initiating update for table template ID: {TemplateId} with Name: {TemplateName}")]
    private static partial void LogUpdatingTableTemplate(ILogger logger, Guid templateId, string templateName);

    [LoggerMessage(EventId = LogEventIds.CannotModifySystemPreset, Level = LogLevel.Warning, Message = "Attempted to modify or delete system preset table template ID: {TemplateId}")]
    private static partial void LogCannotModifySystemPreset(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateUpdatedSuccessfully, Level = LogLevel.Information, Message = "Table template successfully updated with ID: {TemplateId}")]
    private static partial void LogTableTemplateUpdatedSuccessfully(ILogger logger, Guid templateId);
}
