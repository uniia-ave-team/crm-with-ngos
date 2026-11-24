using Crm.Application.Dtos.System;
using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Reads the scheduled maintenance notice from the shared demo database for every visitor. The notice is set with
/// <c>crm notice</c>, which writes to that database. Sandboxes are copies made at startup and never see a later
/// notice, and the process-wide cache would otherwise keep whatever the sandbox of the request that filled it had.
/// </summary>
/// <param name="inner">The regular maintenance notice cache.</param>
public sealed class DemoSharedMaintenanceNoticeCache(IMaintenanceNoticeCache inner) : IMaintenanceNoticeCache
{
    /// <inheritdoc />
    public async Task<MaintenanceNoticeDto?> GetActiveNoticeAsync(CancellationToken cancellationToken = default)
    {
        using (DemoSandboxAccessor.UseSharedDatabase())
        {
            return await inner.GetActiveNoticeAsync(cancellationToken);
        }
    }
}
