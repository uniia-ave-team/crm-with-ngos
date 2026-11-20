using System.Security.Claims;
using Crm.Application.Dtos.User;
using Crm.Application.Interfaces;

namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Writes the identifier of the current sandbox into every access token issued in demo mode,
/// so later requests with that token are served from the same sandbox.
/// </summary>
/// <param name="sandboxAccessor">The accessor of the current sandbox.</param>
public sealed class DemoSandboxClaimsProvider(IDemoSandboxAccessor sandboxAccessor) : IAccessTokenClaimsProvider
{
    /// <inheritdoc />
    public IEnumerable<Claim> GetClaims(UserBasicDto user)
    {
        var sandbox = sandboxAccessor.Current;

        return sandbox is null ? [] : [new Claim(DemoClaimTypes.SandboxId, sandbox.Id)];
    }
}
