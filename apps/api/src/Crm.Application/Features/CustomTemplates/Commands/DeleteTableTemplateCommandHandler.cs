using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomTemplates.Commands;

/// <summary>
/// Handles the <see cref="DeleteTableTemplateCommand"/> to remove a custom table template.
/// System presets are protected from deletion.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and deleting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public partial class DeleteTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<DeleteTableTemplateCommandHandler> logger) : IRequestHandler<DeleteTableTemplateCommand>
{
    public async Task Handle(DeleteTableTemplateCommand request, CancellationToken cancellationToken)
    {
        LogDeletingTableTemplate(logger, request.Id);

        var template = await tableTemplateRepository.GetAsync(request.Id, cancellationToken);

        if (template.IsSystemPreset)
        {
            LogCannotDeleteSystemPreset(logger, request.Id);
            throw new CannotModifySystemPresetException(request.Id);
        }

        await tableTemplateRepository.DeleteAsync(request.Id, cancellationToken);

        LogTableTemplateDeletedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTableTemplate, Level = LogLevel.Information, Message = "Initiating deletion of table template ID: {TemplateId}")]
    private static partial void LogDeletingTableTemplate(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.CannotDeleteSystemPreset, Level = LogLevel.Warning, Message = "Attempted to delete system preset table template ID: {TemplateId}")]
    private static partial void LogCannotDeleteSystemPreset(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateDeletedSuccessfully, Level = LogLevel.Information, Message = "Table template successfully deleted with ID: {TemplateId}")]
    private static partial void LogTableTemplateDeletedSuccessfully(ILogger logger, Guid templateId);
}
