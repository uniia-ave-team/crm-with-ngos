using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Crm.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Owns the demo sandboxes: creates them within the configured limits, finds them by identifier
/// and removes them when they expire.
/// </summary>
public sealed partial class DemoSandboxManager : IDisposable
{
    private const int SandboxIdBytes = 16;

    private readonly ConcurrentDictionary<string, DemoSandbox> _sandboxes = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _createLock = new(1, 1);
    private readonly IDemoSandboxDatabase _database;
    private readonly DemoSandboxOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DemoSandboxManager> _logger;
    private readonly string _uploadRoot;
    private readonly PartitionedRateLimiter<string> _creationLimiter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DemoSandboxManager"/> class.
    /// </summary>
    /// <param name="database">The sandbox database provisioner.</param>
    /// <param name="demoModeOptions">The demo mode options.</param>
    /// <param name="fileStorageOptions">The file storage options.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger.</param>
    public DemoSandboxManager(
        IDemoSandboxDatabase database,
        IOptions<DemoModeOptions> demoModeOptions,
        IOptions<FileStorageOptions> fileStorageOptions,
        TimeProvider timeProvider,
        ILogger<DemoSandboxManager> logger)
    {
        ArgumentNullException.ThrowIfNull(demoModeOptions);
        ArgumentNullException.ThrowIfNull(fileStorageOptions);

        _database = database;
        _options = demoModeOptions.Value.Sandbox;
        _timeProvider = timeProvider;
        _logger = logger;
        _uploadRoot = DemoSandboxPaths.ResolveUploadRoot(fileStorageOptions.Value.UploadDirectory);

        var window = TimeSpan.FromMinutes(_options.NewSandboxWindowMinutes);
        int permits = _options.NewSandboxesPerAddress;

        _creationLimiter = PartitionedRateLimiter.Create<string, string>(address =>
            RateLimitPartition.GetFixedWindowLimiter(
                address,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permits,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }

    /// <summary>
    /// Gets the number of sandboxes that exist right now.
    /// </summary>
    public int Count => _sandboxes.Count;

    /// <summary>
    /// Removes everything left over from a previous run and builds the template database.
    /// Must be called once at startup, after migrations and seeding.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when sandboxes can be created.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        DemoSandboxPaths.DeleteAllSandboxUploads(_uploadRoot);

        await _database.PrepareTemplateAsync(cancellationToken);
    }

    /// <summary>
    /// Checks the per-address limit on new sandboxes and consumes one permit.
    /// </summary>
    /// <param name="clientAddress">The client address the limit is counted for.</param>
    /// <returns><c>false</c> when the address created too many sandboxes recently.</returns>
    public bool TryAcquireCreationPermit(string clientAddress)
    {
        using var lease = _creationLimiter.AttemptAcquire(clientAddress);

        if (!lease.IsAcquired)
        {
            LogCreationRateLimited(_logger, clientAddress);
        }

        return lease.IsAcquired;
    }

    /// <summary>
    /// Finds a live sandbox and records the access. Expired sandboxes are not returned.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="sandbox">The sandbox, when found.</param>
    /// <returns><c>true</c> when the sandbox exists and has not expired.</returns>
    public bool TryGet(string? sandboxId, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out DemoSandbox? sandbox)
    {
        sandbox = null;

        if (string.IsNullOrEmpty(sandboxId) || !_sandboxes.TryGetValue(sandboxId, out var found))
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();

        if (found.IsExpired(now, _options))
        {
            return false;
        }

        found.Touch(now);
        sandbox = found;

        return true;
    }

    /// <summary>
    /// Creates a new sandbox. When the limit is reached, the least recently used sandbox that has been
    /// idle long enough is evicted first.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The new sandbox, or <c>null</c> when there is no room for one.</returns>
    public async Task<DemoSandbox?> CreateAsync(CancellationToken cancellationToken)
    {
        await _createLock.WaitAsync(cancellationToken);

        try
        {
            if (_sandboxes.Count >= _options.MaxSandboxes)
            {
                await RemoveExpiredAsync(cancellationToken);
            }

            if (_sandboxes.Count >= _options.MaxSandboxes && !await TryEvictLeastRecentlyUsedAsync(cancellationToken))
            {
                LogCapacityReached(_logger, _options.MaxSandboxes);
                return null;
            }

            string id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(SandboxIdBytes));
            string databaseName = _database.GetSandboxDatabaseName(id);

            await _database.CreateSandboxAsync(databaseName, cancellationToken);

            var sandbox = new DemoSandbox(id, databaseName, _database.GetConnectionString(databaseName), _timeProvider.GetUtcNow());
            _sandboxes[id] = sandbox;

            LogSandboxCreated(_logger, id, _sandboxes.Count);

            return sandbox;
        }
        finally
        {
            _createLock.Release();
        }
    }

    /// <summary>
    /// Removes a sandbox with its database and uploaded files.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the sandbox is gone.</returns>
    public async Task RemoveAsync(string sandboxId, CancellationToken cancellationToken)
    {
        if (!_sandboxes.TryRemove(sandboxId, out var sandbox))
        {
            return;
        }

        try
        {
            await _database.DropSandboxAsync(sandbox.DatabaseName, cancellationToken);
            DemoSandboxPaths.DeleteSandboxUploads(_uploadRoot, sandbox.Id);

            LogSandboxRemoved(_logger, sandbox.Id, _sandboxes.Count);
        }
#pragma warning disable CA1031 // A failed drop must not break the request or the cleanup loop; startup removes leftovers.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            LogSandboxRemovalFailed(_logger, ex, sandbox.Id);
        }
    }

    /// <summary>
    /// Removes every sandbox that outlived its idle timeout or its maximum lifetime.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The number of removed sandboxes.</returns>
    public async Task<int> RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        int removed = 0;

        foreach (var sandbox in _sandboxes.Values)
        {
            if (sandbox.IsExpired(now, _options))
            {
                await RemoveAsync(sandbox.Id, cancellationToken);
                removed++;
            }
        }

        return removed;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _createLock.Dispose();
        _creationLimiter.Dispose();
    }

    private async Task<bool> TryEvictLeastRecentlyUsedAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();

        var candidate = _sandboxes.Values
            .Where(s => now - s.LastAccess >= _options.EvictableAfterIdle)
            .OrderBy(s => s.LastAccess)
            .FirstOrDefault();

        if (candidate is null)
        {
            return false;
        }

        await RemoveAsync(candidate.Id, cancellationToken);

        return true;
    }

    [LoggerMessage(EventId = DemoLogEventIds.SandboxCreated, Level = LogLevel.Information, Message = "Demo sandbox {SandboxId} created ({Count} in use).")]
    private static partial void LogSandboxCreated(ILogger logger, string sandboxId, int count);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxRemoved, Level = LogLevel.Information, Message = "Demo sandbox {SandboxId} removed ({Count} in use).")]
    private static partial void LogSandboxRemoved(ILogger logger, string sandboxId, int count);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxRemovalFailed, Level = LogLevel.Error, Message = "Demo sandbox {SandboxId} could not be removed completely.")]
    private static partial void LogSandboxRemovalFailed(ILogger logger, Exception exception, string sandboxId);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxCapacityReached, Level = LogLevel.Warning, Message = "Demo sandbox limit of {Limit} reached and nothing can be evicted.")]
    private static partial void LogCapacityReached(ILogger logger, int limit);

    [LoggerMessage(EventId = DemoLogEventIds.SandboxCreationRateLimited, Level = LogLevel.Warning, Message = "Demo sandbox creation rate limit hit for {ClientAddress}.")]
    private static partial void LogCreationRateLimited(ILogger logger, string clientAddress);
}
