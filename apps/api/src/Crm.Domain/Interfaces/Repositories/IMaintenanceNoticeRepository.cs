using Crm.Domain.Entities;

namespace Crm.Domain.Interfaces.Repositories;

/// <summary>
/// Provides persistence operations for the singleton <see cref="MaintenanceNotice"/> record.
/// </summary>
public interface IMaintenanceNoticeRepository
{
    /// <summary>
    /// Retrieves the stored maintenance notice, or <c>null</c> if none is set.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The stored <see cref="MaintenanceNotice"/>, or <c>null</c>.</returns>
    Task<MaintenanceNotice?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or replaces the singleton maintenance notice record in the database.
    /// </summary>
    /// <param name="message">The user-facing maintenance notice message.</param>
    /// <param name="startsAt">The UTC timestamp when maintenance starts.</param>
    /// <param name="endsAt">The UTC timestamp when maintenance ends.</param>
    /// <param name="updatedAt">The UTC timestamp of this update.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpsertAsync(
        string message,
        DateTime startsAt,
        DateTime endsAt,
        DateTime updatedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the stored maintenance notice record if present.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if a notice existed and was deleted; otherwise, <c>false</c>.</returns>
    Task<bool> ClearAsync(CancellationToken cancellationToken = default);
}
