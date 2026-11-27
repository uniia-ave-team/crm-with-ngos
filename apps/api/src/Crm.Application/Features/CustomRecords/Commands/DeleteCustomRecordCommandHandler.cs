using Crm.Application.Common.Consts;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="DeleteCustomRecordCommand"/> to delete an existing custom record directly from the database.
/// Because row order uses fractional indexing, deletion requires zero neighbor updates.
/// </summary>
/// <param name="customRecordRepository">The repository used for deleting custom records.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class DeleteCustomRecordCommandHandler(
    ICustomRecordRepository customRecordRepository,
    ILogger<DeleteCustomRecordCommandHandler> logger) : ICommandHandler<DeleteCustomRecordCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(DeleteCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogDeletingCustomRecord(logger, command.Id);

        await customRecordRepository.DeleteAsync(command.Id, cancellationToken);

        LogCustomRecordDeletedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.DeletingCustomRecord, Level = LogLevel.Information, Message = "Initiating deletion for custom record ID: {RecordId}")]
    private static partial void LogDeletingCustomRecord(ILogger logger, Guid recordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordDeletedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully deleted.")]
    private static partial void LogCustomRecordDeletedSuccessfully(ILogger logger, Guid recordId);
}
