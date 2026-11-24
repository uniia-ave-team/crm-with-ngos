using Crm.Application.Dtos.System;

namespace Crm.Application.Interfaces;

/// <summary>
/// Provides cached read access to the active scheduled maintenance notice.
/// </summary>
public interface IMaintenanceNoticeCache
{
    /// <summary>
    /// Retrieves the active scheduled maintenance notice, or <c>null</c> when no notice is set
    /// or the scheduled maintenance window has already ended.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The active <see cref="MaintenanceNoticeDto"/>, or <c>null</c>.</returns>
    Task<MaintenanceNoticeDto?> GetActiveNoticeAsync(CancellationToken cancellationToken = default);
}
