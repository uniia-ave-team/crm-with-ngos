using System.Globalization;
using System.Security.Claims;
using Crm.Application.Dtos.Users;
using Crm.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Writes the current sandbox into every access token issued in demo mode: its identifier, so later
/// requests with that token are served from the same sandbox, and the moment it is deleted, so the
/// web client can show a countdown.
/// </summary>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
/// <param name="demoModeOptions">The demo mode options.</param>
public sealed class DemoSandboxClaimsProvider(
    IDemoSandboxAccessor sandboxAccessor,
    IOptions<DemoModeOptions> demoModeOptions) : IAccessTokenClaimsProvider
{
    /// <inheritdoc />
    public IEnumerable<Claim> GetClaims(UserBasicDto user)
    {
        var sandbox = sandboxAccessor.Current;

        if (sandbox is null)
        {
            return [];
        }

        long expiresAt = sandbox.GetExpiresAt(demoModeOptions.Value.Sandbox).ToUnixTimeSeconds();

        return
        [
            new Claim(DemoClaimTypes.SandboxId, sandbox.Id),
            new Claim(DemoClaimTypes.SandboxExpiresAt, expiresAt.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
        ];
    }
}
