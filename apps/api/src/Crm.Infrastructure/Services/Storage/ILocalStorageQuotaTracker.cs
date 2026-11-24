namespace Crm.Infrastructure.Services.Storage;

/// <summary>
/// Tracks cached disk usage and in-flight upload reservations per storage root directory
/// to enforce installation-wide storage quotas in O(1) time and clean up stale temporary upload files.
/// </summary>
public interface ILocalStorageQuotaTracker
{
    /// <summary>
    /// Reserves <paramref name="incomingBytes"/> against the installation quota for <paramref name="rootDirectory"/>,
    /// throwing <see cref="Domain.Exceptions.StorageQuotaExceededException"/> if the quota would be exceeded.
    /// </summary>
    /// <param name="rootDirectory">The normalized root upload directory path.</param>
    /// <param name="incomingBytes">The size in bytes of the incoming file upload.</param>
    /// <param name="maxTotalBytes">The maximum permitted total bytes across the root directory.</param>
    void Reserve(string rootDirectory, long incomingBytes, long maxTotalBytes);

    /// <summary>
    /// Finalizes a previously reserved upload, transitioning <paramref name="reservedBytes"/> out of in-flight
    /// accounting and recording the actual <paramref name="writtenBytes"/> committed to disk.
    /// </summary>
    /// <param name="rootDirectory">The normalized root upload directory path.</param>
    /// <param name="reservedBytes">The originally reserved byte count.</param>
    /// <param name="writtenBytes">The actual number of bytes written to disk.</param>
    void Commit(string rootDirectory, long reservedBytes, long writtenBytes);

    /// <summary>
    /// Releases a previously reserved upload when writing fails or is cancelled before commit.
    /// </summary>
    /// <param name="rootDirectory">The normalized root upload directory path.</param>
    /// <param name="reservedBytes">The reserved byte count to release.</param>
    void Release(string rootDirectory, long reservedBytes);

    /// <summary>
    /// Deducts <paramref name="deletedBytes"/> from the cached disk usage of <paramref name="rootDirectory"/>.
    /// </summary>
    /// <param name="rootDirectory">The normalized root upload directory path.</param>
    /// <param name="deletedBytes">The size in bytes of the deleted file.</param>
    void OnFileDeleted(string rootDirectory, long deletedBytes);

    /// <summary>
    /// Removes orphaned temporary upload files (<c>*.tmp</c>) older than <paramref name="maxAge"/>
    /// from the specified storage root directory.
    /// </summary>
    /// <param name="rootDirectory">The root upload directory to scan.</param>
    /// <param name="maxAge">The maximum age before a temporary file is considered stale.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The number of stale temporary files removed.</returns>
    int CleanupStaleTempFiles(string rootDirectory, TimeSpan maxAge, CancellationToken cancellationToken = default);
}
