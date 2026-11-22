using Crm.Application.Common.Consts;
using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.TableTemplates.Commands;

/// <summary>
/// Handles the <see cref="DeleteTableTemplateCommand"/> to remove a table template.
/// System presets are protected from deletion.
/// </summary>
/// <param name="tableTemplateRepository">The repository used for accessing and deleting table template data.</param>
/// <param name="logger">The logger instance for tracking command execution.</param>
public sealed partial class DeleteTableTemplateCommandHandler(
    ITableTemplateRepository tableTemplateRepository,
    ILogger<DeleteTableTemplateCommandHandler> logger) : ICommandHandler<DeleteTableTemplateCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteTableTemplateCommand command, CancellationToken cancellationToken)
    {
        LogDeletingTableTemplate(logger, command.Id);

        await tableTemplateRepository.LockForUpdateAsync(command.Id, cancellationToken);
        var template = await tableTemplateRepository.GetAsync(command.Id, cancellationToken);

        if (template.IsSystemPreset)
        {
            LogCannotDeleteSystemPreset(logger, command.Id);
            throw new CannotModifySystemPresetException(command.Id);
        }

        await tableTemplateRepository.DeleteAsync(command.Id, cancellationToken);

        LogTableTemplateDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingTableTemplate, Level = LogLevel.Information, Message = "Initiating deletion of table template ID: {TemplateId}")]
    private static partial void LogDeletingTableTemplate(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.CannotDeleteSystemPreset, Level = LogLevel.Warning, Message = "Attempted to delete system preset table template ID: {TemplateId}")]
    private static partial void LogCannotDeleteSystemPreset(ILogger logger, Guid templateId);

    [LoggerMessage(EventId = LogEventIds.TableTemplateDeletedSuccessfully, Level = LogLevel.Information, Message = "Table template successfully deleted with ID: {TemplateId}")]
    private static partial void LogTableTemplateDeletedSuccessfully(ILogger logger, Guid templateId);
}
