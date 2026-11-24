using System.Collections.Concurrent;
using Crm.Domain.Exceptions;
using Crm.Infrastructure.Consts;

namespace Crm.Infrastructure.Services.Storage;

/// <summary>
/// Thread-safe, DI-managed implementation of <see cref="ILocalStorageQuotaTracker"/> that tracks cached disk usage
/// and in-flight upload reservations per storage root directory without holding locks across disk scans.
/// </summary>
/// <param name="timeProvider">The time provider supplying current UTC timestamps for stale-file expiration checks.</param>
public sealed class LocalStorageQuotaTracker(TimeProvider timeProvider) : ILocalStorageQuotaTracker
{
    private static readonly TimeSpan StaleTempFileMaxAge = TimeSpan.FromHours(LocalFileStorageConstants.StaleTempFileMaxAgeHours);

    private readonly ConcurrentDictionary<string, DirectoryQuotaState> _states =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <inheritdoc />
    public void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes)
    {
        DirectoryQuotaState state = _states.GetOrAdd(rootDirectory, _ => new DirectoryQuotaState());
        state.Reserve(rootDirectory, incomingBytes, maxTotalBytes, timeProvider);
    }

    /// <inheritdoc />
    public void Commit(string rootDirectory, long reservedBytes, long writtenBytes)
    {
        if (_states.TryGetValue(rootDirectory, out var state))
        {
            state.Commit(reservedBytes, writtenBytes);
        }
    }

    /// <inheritdoc />
    public void Release(string rootDirectory, long reservedBytes)
    {
        if (_states.TryGetValue(rootDirectory, out var state))
        {
            state.Release(reservedBytes);
        }
    }

    /// <inheritdoc />
    public void OnFileDeleted(string rootDirectory, long deletedBytes)
    {
        if (deletedBytes > 0 && _states.TryGetValue(rootDirectory, out var state))
        {
            state.OnFileDeleted(deletedBytes);
        }
    }

    /// <inheritdoc />
    public int CleanupStaleTempFiles(string rootDirectory, TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        DateTime cutoffUtc = timeProvider.GetUtcNow().UtcDateTime - maxAge;
        int deletedCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(rootDirectory, $"*{LocalFileStorageConstants.TempFileExtension}", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryDeleteIfStale(filePath, cutoffUtc))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private static long CalculateDirectorySize(string rootDirectory, TimeProvider timeProvider)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        DateTime staleCutoffUtc = timeProvider.GetUtcNow().UtcDateTime - StaleTempFileMaxAge;
        long totalBytes = 0;

        foreach (string filePath in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(LocalFileStorageConstants.TempFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                TryDeleteIfStale(filePath, staleCutoffUtc);
                continue;
            }

            try
            {
                totalBytes += new FileInfo(filePath).Length;
            }
            catch (FileNotFoundException)
            {
                // Ignore files deleted concurrently during directory traversal.
            }
            catch (IOException)
            {
                // Ignore transient I/O errors on concurrently modified files.
            }
        }

        return totalBytes;
    }

    private static bool TryDeleteIfStale(string filePath, DateTime cutoffUtc)
    {
        try
        {
            if (File.GetLastWriteTimeUtc(filePath) <= cutoffUtc)
            {
                File.Delete(filePath);
                return true;
            }
        }
        catch (FileNotFoundException)
        {
            // Ignore files already moved or deleted concurrently.
        }
        catch (IOException)
        {
            // Ignore files currently locked by an active upload.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of abandoned temporary upload file.
        }

        return false;
    }

    private sealed class DirectoryQuotaState
    {
        private readonly Lock _syncRoot = new();
        private bool _initialized;
        private long _usedBytes;
        private long _inFlightBytes;

        public void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes, TimeProvider timeProvider)
        {
            if (incomingBytes > maxTotalBytes)
            {
                throw CreateQuotaExceededException(maxTotalBytes);
            }

            bool needsInitialScan;
            lock (_syncRoot)
            {
                needsInitialScan = !_initialized;
            }

            if (needsInitialScan)
            {
                long scannedBytes = CalculateDirectorySize(rootDirectory, timeProvider);
                lock (_syncRoot)
                {
                    if (!_initialized)
                    {
                        _usedBytes = scannedBytes;
                        _initialized = true;
                    }
                }
            }

            bool needsRescan;
            lock (_syncRoot)
            {
                if (_usedBytes + _inFlightBytes <= maxTotalBytes - incomingBytes)
                {
                    _inFlightBytes += incomingBytes;
                    return;
                }

                needsRescan = true;
            }

            if (needsRescan)
            {
                // Re-scan disk outside the accounting lock so concurrent commits/releases are never stalled by I/O.
                long refreshedBytes = CalculateDirectorySize(rootDirectory, timeProvider);

                lock (_syncRoot)
                {
                    _usedBytes = refreshedBytes;

                    if (_usedBytes + _inFlightBytes > maxTotalBytes - incomingBytes)
                    {
                        throw CreateQuotaExceededException(maxTotalBytes);
                    }

                    _inFlightBytes += incomingBytes;
                }
            }
        }

        public void Commit(long reservedBytes, long writtenBytes)
        {
            lock (_syncRoot)
            {
                _inFlightBytes = Math.Max(0, _inFlightBytes - reservedBytes);
                _usedBytes += writtenBytes;
            }
        }

        public void Release(long reservedBytes)
        {
            lock (_syncRoot)
            {
                _inFlightBytes = Math.Max(0, _inFlightBytes - reservedBytes);
            }
        }

        public void OnFileDeleted(long deletedBytes)
        {
            lock (_syncRoot)
            {
                if (_initialized)
                {
                    _usedBytes = Math.Max(0, _usedBytes - deletedBytes);
                }
            }
        }

        private static StorageQuotaExceededException CreateQuotaExceededException(long maxTotalBytes) =>
            new($"File storage quota of {maxTotalBytes} bytes exceeded. Delete unused files or contact your administrator.");
    }
}
