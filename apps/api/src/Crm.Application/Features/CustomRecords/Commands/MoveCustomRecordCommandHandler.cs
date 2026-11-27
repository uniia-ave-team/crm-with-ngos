using Crm.Application.Common.Consts;
using Crm.Application.Interfaces;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.CustomRecords.Commands;

/// <summary>
/// Handles the <see cref="MoveCustomRecordCommand"/> to reposition a custom record within its table's manual order (drag-and-drop).
/// </summary>
/// <param name="customRecordPositionService">The shared service managing fractional-index record positioning.</param>
/// <param name="logger">The logger instance for tracking execution flow.</param>
public sealed partial class MoveCustomRecordCommandHandler(
    ICustomRecordPositionService customRecordPositionService,
    ILogger<MoveCustomRecordCommandHandler> logger) : ICommandHandler<MoveCustomRecordCommand>
{
    /// <inheritdoc />
    public async ValueTask<Unit> Handle(MoveCustomRecordCommand command, CancellationToken cancellationToken)
    {
        LogMovingCustomRecord(logger, command.Id, command.PreviousRecordId, command.NextRecordId);

        await customRecordPositionService.MoveAsync(
            command.Id,
            command.PreviousRecordId,
            command.NextRecordId,
            cancellationToken);

        LogCustomRecordMovedSuccessfully(logger, command.Id);

        return Unit.Value;
    }

    [LoggerMessage(EventId = LogEventIds.MovingCustomRecord, Level = LogLevel.Information, Message = "Initiating position move for custom record ID: {RecordId} between PreviousRecordId: {PreviousRecordId} and NextRecordId: {NextRecordId}")]
    private static partial void LogMovingCustomRecord(ILogger logger, Guid recordId, Guid? previousRecordId, Guid? nextRecordId);

    [LoggerMessage(EventId = LogEventIds.CustomRecordMovedSuccessfully, Level = LogLevel.Information, Message = "Custom record ID: {RecordId} successfully repositioned.")]
    private static partial void LogCustomRecordMovedSuccessfully(ILogger logger, Guid recordId);
}
