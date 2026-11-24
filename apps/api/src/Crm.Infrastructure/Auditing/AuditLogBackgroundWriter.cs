using System.Threading.Channels;
using Crm.Application.Common.Consts;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Background worker that continuously drains <see cref="ChannelAuditLogBuffer"/> in configurable batches
/// and persists them through the registered <see cref="IAuditLogSink"/>.
/// Sink failures are logged and isolated so audit logging never disrupts the host process.
/// </summary>
/// <param name="buffer">The in-memory audit record buffer.</param>
/// <param name="sink">The pluggable audit log storage sink.</param>
/// <param name="options">The auditing configuration options.</param>
/// <param name="logger">The logger instance.</param>
public sealed partial class AuditLogBackgroundWriter(
    ChannelAuditLogBuffer buffer,
    IAuditLogSink sink,
    IOptions<AuditOptions> options,
    ILogger<AuditLogBackgroundWriter> logger) : BackgroundService
{
    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        buffer.Complete();
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogAuditWriterStarting(logger);

        int batchSize = Math.Max(1, options.Value.BatchSize);
        var flushInterval = TimeSpan.FromMilliseconds(Math.Max(50, options.Value.FlushIntervalMilliseconds));
        var batch = new List<EntityAuditRecord>(batchSize);
        var reader = buffer.Reader;

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                await CollectBatchAsync(reader, batch, batchSize, flushInterval, stoppingToken);

                if (batch.Count > 0)
                {
                    await FlushBatchSafeAsync(batch, CancellationToken.None);
                    batch.Clear();
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogAuditWriterStopping(logger);
        }
        catch (Exception ex)
        {
            LogAuditWriterFatalError(logger, ex);
        }
        finally
        {
            await DrainRemainingAsync(reader, batch, batchSize);
        }
    }

    private static async Task CollectBatchAsync(
        ChannelReader<EntityAuditRecord> reader,
        List<EntityAuditRecord> batch,
        int batchSize,
        TimeSpan flushInterval,
        CancellationToken stoppingToken)
    {
        while (batch.Count < batchSize && reader.TryRead(out var item))
        {
            batch.Add(item);
        }

        if (batch.Count == 0 || batch.Count >= batchSize)
        {
            return;
        }

        using var flushTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        flushTimeoutCts.CancelAfter(flushInterval);

        try
        {
            while (batch.Count < batchSize && await reader.WaitToReadAsync(flushTimeoutCts.Token))
            {
                while (batch.Count < batchSize && reader.TryRead(out var item))
                {
                    batch.Add(item);
                }
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Flush interval elapsed; proceed with the partial batch collected so far.
        }
    }

    private async Task DrainRemainingAsync(
        ChannelReader<EntityAuditRecord> reader,
        List<EntityAuditRecord> batch,
        int batchSize)
    {
        while (reader.TryRead(out var item))
        {
            batch.Add(item);

            if (batch.Count >= batchSize)
            {
                await FlushBatchSafeAsync(batch, CancellationToken.None);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            await FlushBatchSafeAsync(batch, CancellationToken.None);
            batch.Clear();
        }
    }

    private async Task FlushBatchSafeAsync(List<EntityAuditRecord> batch, CancellationToken cancellationToken)
    {
        try
        {
            await sink.WriteBatchAsync(batch, cancellationToken);
        }
        catch (Exception ex)
        {
            LogAuditBatchWriteFailed(logger, batch.Count, ex);
        }
    }

    [LoggerMessage(
        EventId = LogEventIds.AuditWriterStarting,
        Level = LogLevel.Information,
        Message = "Entity audit log background writer is starting.")]
    private static partial void LogAuditWriterStarting(ILogger logger);

    [LoggerMessage(
        EventId = LogEventIds.AuditWriterStopping,
        Level = LogLevel.Information,
        Message = "Entity audit log background writer is stopping and draining buffered records.")]
    private static partial void LogAuditWriterStopping(ILogger logger);

    [LoggerMessage(
        EventId = LogEventIds.AuditWriterFatalError,
        Level = LogLevel.Critical,
        Message = "A fatal error occurred in the entity audit log background writer.")]
    private static partial void LogAuditWriterFatalError(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = LogEventIds.AuditBatchWriteFailed,
        Level = LogLevel.Error,
        Message = "Failed to persist batch of {BatchSize} entity audit log records.")]
    private static partial void LogAuditBatchWriteFailed(ILogger logger, int batchSize, Exception exception);
}
