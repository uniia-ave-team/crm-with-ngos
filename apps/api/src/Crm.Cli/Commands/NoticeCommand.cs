using System.Globalization;
using Crm.Application.Interfaces;
using Crm.Cli.Abstractions;
using Crm.Cli.Constants;
using Crm.Cli.Models;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Crm.Cli.Commands;

/// <summary>
/// Manages the scheduled maintenance notice stored in the database (<c>crm notice set ...</c> and <c>crm notice clear</c>).
/// </summary>
public partial class NoticeCommand(
    IMaintenanceNoticeRepository maintenanceNoticeRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<NoticeCommand> logger) : ICliCommand
{
    private const string SetSubcommand = "set";
    private const string ClearSubcommand = "clear";
    private const string MessageOption = "message";
    private const string MessageShort = "m";
    private const string StartsAtOption = "starts-at";
    private const string StartsAtShort = "s";
    private const string EndsAtOption = "ends-at";
    private const string EndsAtShort = "e";

    /// <inheritdoc />
    public string Name => "notice";

    /// <inheritdoc />
    public string Description =>
        "Sets or clears the scheduled maintenance notice (set --message ... --starts-at ... --ends-at ... | clear).";

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(CliArguments args, CancellationToken cancellationToken = default)
    {
        var subcommand = args.Operands.Count > 0 ? args.Operands[0] : null;

        if (string.Equals(subcommand, SetSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteSetAsync(args, cancellationToken);
        }

        if (string.Equals(subcommand, ClearSubcommand, StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteClearAsync(cancellationToken);
        }

        LogInvalidSubcommand(logger, subcommand ?? "<none>");
        return 1;
    }

    private async Task<int> ExecuteSetAsync(CliArguments args, CancellationToken cancellationToken)
    {
        var rawMessage = args.GetValue(MessageOption, MessageShort);
        var rawStartsAt = args.GetValue(StartsAtOption, StartsAtShort);
        var rawEndsAt = args.GetValue(EndsAtOption, EndsAtShort);

        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            LogValidationFailed(logger, "A non-empty message is required. Use --message <text>.");
            return 1;
        }

        var message = rawMessage.Trim();
        if (message.Length > MaintenanceNoticeValidationConstants.MaxMessageLength)
        {
            LogValidationFailed(
                logger,
                $"Message must not exceed {MaintenanceNoticeValidationConstants.MaxMessageLength} characters.");
            return 1;
        }

        if (!TryParseUtcTimestamp(rawStartsAt, out var startsAt))
        {
            LogValidationFailed(
                logger,
                "A valid ISO-8601 start timestamp is required. Use --starts-at <YYYY-MM-DDTHH:MM:SSZ>.");
            return 1;
        }

        if (!TryParseUtcTimestamp(rawEndsAt, out var endsAt))
        {
            LogValidationFailed(
                logger,
                "A valid ISO-8601 end timestamp is required. Use --ends-at <YYYY-MM-DDTHH:MM:SSZ>.");
            return 1;
        }

        if (endsAt <= startsAt)
        {
            LogValidationFailed(logger, "End time (--ends-at) must be after start time (--starts-at).");
            return 1;
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        if (endsAt <= utcNow)
        {
            LogEndTimeInPast(logger, endsAt);
        }

        try
        {
            await maintenanceNoticeRepository.UpsertAsync(message, startsAt, endsAt, utcNow, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            LogNoticeSet(logger, startsAt, endsAt);
            return 0;
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex);
            return 1;
        }
    }

    private async Task<int> ExecuteClearAsync(CancellationToken cancellationToken)
    {
        try
        {
            bool cleared = await maintenanceNoticeRepository.ClearAsync(cancellationToken);

            if (cleared)
            {
                LogNoticeCleared(logger);
            }
            else
            {
                LogNoticeAlreadyClear(logger);
            }

            return 0;
        }
        catch (Exception ex)
        {
            LogUnexpectedError(logger, ex);
            return 1;
        }
    }

    private static bool TryParseUtcTimestamp(string? raw, out DateTime utcDateTime)
    {
        if (string.IsNullOrWhiteSpace(raw)
            || !DateTimeOffset.TryParse(
                raw.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var dto))
        {
            utcDateTime = default;
            return false;
        }

        utcDateTime = dto.UtcDateTime;
        return true;
    }

    [LoggerMessage(EventId = CliLogEventIds.NoticeInvalidSubcommand, Level = LogLevel.Error, Message = "Unknown or missing notice subcommand '{Subcommand}'. Use 'crm notice set --message \"...\" --starts-at <ISO-8601> --ends-at <ISO-8601>' or 'crm notice clear'.")]
    private static partial void LogInvalidSubcommand(ILogger logger, string subcommand);

    [LoggerMessage(EventId = CliLogEventIds.NoticeValidationFailed, Level = LogLevel.Error, Message = "Invalid maintenance notice arguments: {Reason}")]
    private static partial void LogValidationFailed(ILogger logger, string reason);

    [LoggerMessage(EventId = CliLogEventIds.NoticeEndTimeInPast, Level = LogLevel.Warning, Message = "The specified end time '{EndsAt:O}' is already in the past; the API will not display this notice.")]
    private static partial void LogEndTimeInPast(ILogger logger, DateTime endsAt);

    [LoggerMessage(EventId = CliLogEventIds.NoticeSetSuccess, Level = LogLevel.Information, Message = "Maintenance notice saved for window {StartsAt:O} to {EndsAt:O}.")]
    private static partial void LogNoticeSet(ILogger logger, DateTime startsAt, DateTime endsAt);

    [LoggerMessage(EventId = CliLogEventIds.NoticeCleared, Level = LogLevel.Information, Message = "Maintenance notice has been cleared.")]
    private static partial void LogNoticeCleared(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.NoticeAlreadyClear, Level = LogLevel.Information, Message = "No maintenance notice was set.")]
    private static partial void LogNoticeAlreadyClear(ILogger logger);

    [LoggerMessage(EventId = CliLogEventIds.NoticeUnexpectedError, Level = LogLevel.Error, Message = "An unexpected error occurred while updating the maintenance notice.")]
    private static partial void LogUnexpectedError(ILogger logger, Exception exception);
}
