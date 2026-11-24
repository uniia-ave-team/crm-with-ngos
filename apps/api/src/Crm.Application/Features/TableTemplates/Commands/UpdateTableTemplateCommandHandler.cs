using Crm.Application.Common.Consts;
using Crm.Application.Extensions;
using Crm.Domain.Entities;
using Crm.Domain.Exceptions;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;
using Mapster;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Handles the <see cref="UpdateTableTemplateCommand"/> to update an existing table template blueprint and synchronize its fields.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and persisting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class UpdateTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<UpdateTableTemplateCommandHandler> logger) : ICommandHandler<UpdateTableTemplateCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(UpdateTableTemplateCommand command, CancellationToken cancellationToken)
    {
        LogUpdatingTableTemplate(logger, command.Id, command.Name);

        await tableTemplateRepository.LockForUpdateAsync(command.Id, cancellationToken);
        var template = await tableTemplateRepository.GetForUpdateWithFieldsAsync(command.Id, cancellationToken);

        if (template.IsSystemPreset)
        {
            LogCannotModifySystemPreset(logger, template.Id);
            throw new CannotModifySystemPresetException(template.Id);
        }

        command.Adapt(template);

        FieldSchemaSynchronizer.Synchronize(
            template.Fields,
            command.Fields,
            id => new TemplateFieldNotFoundException(id, template.Id),
            dto => dto.Adapt<TemplateField>(),
            (existing, dto) => dto.Adapt(existing));

        template.Fields.EnsureValidFieldSchema();

        await tableTemplateRepository.ResolveFieldKeyCollisionsAsync(template.Id, cancellationToken);

        LogTableTemplateUpdatedSuccessfully(logger, template.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.UpdatingTableTemplate, Level = LogLevel.Information, Message = "Initiating update for table template ID: {TemplateId} with Name: {TemplateName}")]
    private static partial void LogUpdatingTableTemplate(ILogger logger, Guid templateId, string templateName);

    [LoggerMessage(EventId = LogEventIds.CannotModifySystemPreset, Level = LogLevel.Warning, Message = "Attempted to modify system preset table template ID: {TemplateId}")]
    private static partial void LogCannotModifySystemPreset(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateUpdatedSuccessfully, Level = LogLevel.Information, Message = "Table template successfully updated with ID: {TemplateId}")]
    private static partial void LogTableTemplateUpdatedSuccessfully(ILogger logger, Guid templateId);
}
