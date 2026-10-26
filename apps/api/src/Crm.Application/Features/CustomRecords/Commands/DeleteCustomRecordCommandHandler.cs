using Crm.Application.Common.Consts;
using Crm.Application.Dtos.CustomRecords.Commands;
using Crm.Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="DeleteCustomRecordCommand"/> to remove an existing custom record from the database.
/// </summary>
/// <param name="customRecordRepository">The repository used for fetching and deleting custom record entities.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public partial class DeleteCustomRecordCommandHandler(
    ICustomRecordRepository customRecordRepository,
    ILogger<DeleteCustomRecordCommandHandler> logger) : IRequestHandler<DeleteCustomRecordCommand>
{
    public async Task Handle(DeleteCustomRecordCommand request, CancellationToken cancellationToken)
    {
        LogDeletingCustomRecord(logger, request.Id);

        await customRecordRepository.DeleteAsync(request.Id, cancellationToken);

        LogCustomRecordDeletedSuccessfully(logger, request.Id);
    }

    [LoggerMessage(EventId = LogEventIds.DeletingCustomRecord, Level = LogLevel.Information, Message = "Initiating deletion for custom record ID: {RecordId}")]
    private static partial void LogDeletingCustomRecord(ILogger logger, Guid recordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordDeletedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully deleted.")]
    private static partial void LogCustomRecordDeletedSuccessfully(ILogger logger, Guid recordId);
}
