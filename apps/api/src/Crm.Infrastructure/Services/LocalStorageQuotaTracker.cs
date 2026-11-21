using System.Collections.Concurrent;
using Crm.Domain.Consts;
using Crm.Domain.Exceptions;

namespace Crm.Infrastructure.Services;

/// <summary>
/// Tracks cached disk usage and in-flight upload reservations per storage root directory
/// to enforce installation-wide storage quotas in O(1) time and clean up stale temporary upload files.
/// </summary>
internal sealed class LocalStorageQuotaTracker
{
    private static readonly TimeSpan StaleTempFileMaxAge = TimeSpan.FromHours(FileStorageConstants.StaleTempFileMaxAgeHours);

    private static readonly ConcurrentDictionary<string, DirectoryQuotaState> States =
        new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Reserves <paramref name="incomingBytes"/> against the installation quota for <paramref name="rootDirectory"/>,
    /// throwing <see cref="StorageQuotaExceededException"/> if the quota would be exceeded.
    /// </summary>
    public static void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes)
    {
        DirectoryQuotaState state = States.GetOrAdd(rootDirectory, _ => new DirectoryQuotaState());
        state.Reserve(rootDirectory, incomingBytes, maxTotalBytes);
    }

    /// <summary>
    /// Finalizes a previously reserved upload, transitioning <paramref name="reservedBytes"/> out of in-flight
    /// accounting and recording the actual <paramref name="writtenBytes"/> committed to disk.
    /// </summary>
    public static void Commit(string rootDirectory, long reservedBytes, long writtenBytes)
    {
        if (States.TryGetValue(rootDirectory, out var state))
        {
            state.Commit(reservedBytes, writtenBytes);
        }
    }

    /// <summary>
    /// Releases a previously reserved upload when writing fails or is cancelled before commit.
    /// </summary>
    public static void Release(string rootDirectory, long reservedBytes)
    {
        if (States.TryGetValue(rootDirectory, out var state))
        {
            state.Release(reservedBytes);
        }
    }

    /// <summary>
    /// Deducts <paramref name="deletedBytes"/> from the cached disk usage of <paramref name="rootDirectory"/>.
    /// </summary>
    public static void OnFileDeleted(string rootDirectory, long deletedBytes)
    {
        if (deletedBytes > 0 && States.TryGetValue(rootDirectory, out var state))
        {
            state.OnFileDeleted(deletedBytes);
        }
    }

    /// <summary>
    /// Removes orphaned temporary upload files (<c>*.tmp</c>) older than <paramref name="maxAge"/>
    /// from the specified storage root directory.
    /// </summary>
    public static int CleanupStaleTempFiles(string rootDirectory, TimeSpan maxAge, DateTime? utcNow = null)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        DateTime cutoffUtc = (utcNow ?? DateTime.UtcNow) - maxAge;
        int deletedCount = 0;

        foreach (string filePath in Directory.EnumerateFiles(rootDirectory, $"*{FileStorageConstants.TempFileExtension}", SearchOption.AllDirectories))
        {
            if (TryDeleteIfStale(filePath, cutoffUtc))
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private static long CalculateDirectorySize(string rootDirectory)
    {
        if (!Directory.Exists(rootDirectory))
        {
            return 0;
        }

        DateTime staleCutoffUtc = DateTime.UtcNow - StaleTempFileMaxAge;
        long totalBytes = 0;

        foreach (string filePath in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
        {
            if (filePath.EndsWith(FileStorageConstants.TempFileExtension, StringComparison.OrdinalIgnoreCase))
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

        public void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes)
        {
            if (incomingBytes > maxTotalBytes)
            {
                throw CreateQuotaExceededException(maxTotalBytes);
            }

            lock (_syncRoot)
            {
                if (!_initialized)
                {
                    _usedBytes = CalculateDirectorySize(rootDirectory);
                    _initialized = true;
                }

                if (_usedBytes + _inFlightBytes > maxTotalBytes - incomingBytes)
                {
                    // Re-scan disk only when cached usage appears full, picking up any out-of-band deletions.
                    _usedBytes = CalculateDirectorySize(rootDirectory);

                    if (_usedBytes + _inFlightBytes > maxTotalBytes - incomingBytes)
                    {
                        throw CreateQuotaExceededException(maxTotalBytes);
                    }
                }

                _inFlightBytes += incomingBytes;
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
