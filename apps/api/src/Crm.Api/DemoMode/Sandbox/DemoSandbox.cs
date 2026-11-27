using System.Collections.Concurrent;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// A visitor's private, short-lived copy of the demo data: its own database plus its own upload folder.
/// </summary>
/// <param name="id">The random sandbox identifier (lowercase hex).</param>
/// <param name="databaseName">The name of the sandbox database.</param>
/// <param name="connectionString">The connection string of the sandbox database.</param>
/// <param name="createdAt">The moment the sandbox was created.</param>
public sealed class DemoSandbox(string id, string databaseName, string connectionString, DateTimeOffset createdAt)
{
    private readonly Lock _uploadLock = new();
    private readonly ConcurrentDictionary<Guid, byte> _revokedUsers = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _tokensRevokedBefore = new();
    private long _lastAccessTicks = createdAt.UtcTicks;
    private long _uploadedBytes;
    private int _uploadedFiles;
    private int _rolePermissionsLoaded;

    /// <summary>
    /// Gets the random sandbox identifier (lowercase hex).
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Gets the name of the sandbox database.
    /// </summary>
    public string DatabaseName { get; } = databaseName;

    /// <summary>
    /// Gets the connection string of the sandbox database.
    /// </summary>
    public string ConnectionString { get; } = connectionString;

    /// <summary>
    /// Gets the moment the sandbox was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; } = createdAt;

    /// <summary>
    /// Gets the moment of the last request served from this sandbox.
    /// </summary>
    public DateTimeOffset LastAccess => new(Interlocked.Read(ref _lastAccessTicks), TimeSpan.Zero);

    /// <summary>
    /// Gets the role permissions of this sandbox. Roles differ between sandboxes, so they are never
    /// stored in the process-wide permission cache.
    /// </summary>
    public ConcurrentDictionary<Guid, IReadOnlySet<string>> RolePermissions { get; } = new();

    /// <summary>
    /// Gets a value indicating whether <see cref="RolePermissions"/> was filled from the sandbox database.
    /// </summary>
    public bool RolePermissionsLoaded => Volatile.Read(ref _rolePermissionsLoaded) == 1;

    /// <summary>
    /// Rejects the access tokens a user already holds in this sandbox (the user was deactivated or deleted here).
    /// User ids are copied from the template, so the same id exists in every other sandbox, where nothing changes.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public void RevokeUser(Guid userId) => _revokedUsers[userId] = 0;

    /// <summary>
    /// Rejects the access tokens a user received at or before <paramref name="cutoff"/> in this sandbox (the
    /// user's roles changed here), so the client refreshes them and picks up the new permissions. A later cutoff
    /// replaces an earlier one; tokens issued after it stay valid.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cutoff">The moment of the role change.</param>
    public void RevokeTokensIssuedBefore(Guid userId, DateTimeOffset cutoff)
        => _tokensRevokedBefore.AddOrUpdate(userId, cutoff, (_, existing) => existing > cutoff ? existing : cutoff);

    /// <summary>
    /// Accepts the access tokens of a user again (the user was activated in this sandbox). Like the regular cache,
    /// this also forgets the cutoff of an earlier role change.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    public void UnrevokeUser(Guid userId)
    {
        _revokedUsers.TryRemove(userId, out _);
        _tokensRevokedBefore.TryRemove(userId, out _);
    }

    /// <summary>
    /// Checks whether the access tokens of a user are rejected in this sandbox.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns><c>true</c> when the user was deactivated or deleted in this sandbox.</returns>
    public bool IsUserRevoked(Guid userId) => _revokedUsers.ContainsKey(userId);

    /// <summary>
    /// Checks whether one access token of a user is rejected in this sandbox: the user was deactivated or
    /// deleted here, or the token was issued at or before the user's last role change here. A token without
    /// an issue time is rejected whenever such a cutoff exists, as in the regular cache.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tokenIssuedAt">When the token was issued, if known.</param>
    /// <returns><c>true</c> when the token must be rejected.</returns>
    public bool IsTokenRevoked(Guid userId, DateTimeOffset? tokenIssuedAt)
        => IsUserRevoked(userId)
            || (_tokensRevokedBefore.TryGetValue(userId, out var cutoff) && (tokenIssuedAt is null || tokenIssuedAt.Value <= cutoff));

    /// <summary>
    /// Records a request served from this sandbox.
    /// </summary>
    /// <param name="now">The current time.</param>
    public void Touch(DateTimeOffset now) => Interlocked.Exchange(ref _lastAccessTicks, now.UtcTicks);

    /// <summary>
    /// Marks <see cref="RolePermissions"/> as filled from the sandbox database.
    /// </summary>
    public void MarkRolePermissionsLoaded() => Volatile.Write(ref _rolePermissionsLoaded, 1);

    /// <summary>
    /// Gets the moment the sandbox is deleted: a fixed time after its creation, whatever the activity.
    /// </summary>
    /// <param name="options">The sandbox limits.</param>
    /// <returns>The expiry time.</returns>
    public DateTimeOffset GetExpiresAt(DemoSandboxOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return CreatedAt + options.Lifetime;
    }

    /// <summary>
    /// Checks whether the sandbox reached the end of its lifetime.
    /// </summary>
    /// <param name="now">The current time.</param>
    /// <param name="options">The sandbox limits.</param>
    /// <returns><c>true</c> when the sandbox must be removed.</returns>
    public bool IsExpired(DateTimeOffset now, DemoSandboxOptions options) => now >= GetExpiresAt(options);

    /// <summary>
    /// Reserves room for one uploaded file within the sandbox upload quota.
    /// </summary>
    /// <param name="bytes">The size of the file.</param>
    /// <param name="maxFiles">The maximum number of files per sandbox.</param>
    /// <param name="maxBytes">The maximum total size per sandbox.</param>
    /// <returns><c>false</c> when the file does not fit into the quota.</returns>
    public bool TryReserveUpload(long bytes, int maxFiles, long maxBytes)
    {
        lock (_uploadLock)
        {
            if (_uploadedFiles + 1 > maxFiles || _uploadedBytes + bytes > maxBytes)
            {
                return false;
            }

            _uploadedFiles++;
            _uploadedBytes += bytes;

            return true;
        }
    }

    /// <summary>
    /// Returns a reservation made by <see cref="TryReserveUpload"/> when the upload failed.
    /// </summary>
    /// <param name="bytes">The size of the file.</param>
    public void ReleaseUpload(long bytes)
    {
        lock (_uploadLock)
        {
            _uploadedFiles = Math.Max(0, _uploadedFiles - 1);
            _uploadedBytes = Math.Max(0, _uploadedBytes - bytes);
        }
    }
}
