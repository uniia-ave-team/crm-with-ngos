using System.Threading.Channels;
using Crm.Application.Common.Consts;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Auditing;

/// <summary>
/// Bounded, non-blocking in-memory <see cref="Channel{T}"/> buffer that decouples entity audit capture
/// from downstream sink persistence so business transactions are never slowed down or failed by audit I/O.
/// </summary>
public sealed partial class ChannelAuditLogBuffer : IAuditLogBuffer
{
    private readonly Channel<EntityAuditRecord> _channel;
    private readonly ILogger<ChannelAuditLogBuffer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChannelAuditLogBuffer"/> class.
    /// </summary>
    /// <param name="options">The auditing configuration options.</param>
    /// <param name="logger">The logger instance for reporting dropped audit entries under backpressure.</param>
    public ChannelAuditLogBuffer(IOptions<AuditOptions> options, ILogger<ChannelAuditLogBuffer> logger)
    {
        _logger = logger;

        int capacity = Math.Max(1, options.Value.QueueCapacity);
        var channelOptions = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        };

        _channel = Channel.CreateBounded<EntityAuditRecord>(channelOptions, OnItemDropped);
    }

    /// <summary>
    /// Gets the channel reader consumed by <see cref="AuditLogBackgroundWriter"/>.
    /// </summary>
    internal ChannelReader<EntityAuditRecord> Reader => _channel.Reader;

    /// <inheritdoc />
    public void Enqueue(EntityAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        _channel.Writer.TryWrite(record);
    }

    /// <inheritdoc />
    public void EnqueueRange(IEnumerable<EntityAuditRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        foreach (var record in records)
        {
            _channel.Writer.TryWrite(record);
        }
    }

    /// <summary>
    /// Marks the underlying channel writer as complete so consumers can drain remaining items and exit.
    /// </summary>
    internal void Complete() => _channel.Writer.TryComplete();

    [LoggerMessage(
        EventId = LogEventIds.AuditQueueItemDropped,
        Level = LogLevel.Warning,
        Message = "Audit log buffer reached capacity; dropped oldest record {AuditRecordId} for {EntityType} ({EntityKey}).")]
    private static partial void LogAuditQueueItemDropped(
        ILogger logger,
        Guid auditRecordId,
        string entityType,
        string entityKey);

    private void OnItemDropped(EntityAuditRecord dropped)
        => LogAuditQueueItemDropped(_logger, dropped.Id, dropped.EntityType, dropped.EntityKey);
}
