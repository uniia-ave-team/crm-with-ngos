namespace Crm.Api.DemoMode;

/// <summary>
/// Claims that demo mode adds to access tokens.
/// </summary>
public static class DemoClaimTypes
{
    /// <summary>
    /// The identifier of the sandbox the token was issued in.
    /// </summary>
    public const string SandboxId = "demo_sid";

    /// <summary>
    /// The Unix time in seconds at which the sandbox and everything the visitor changed in it are deleted.
    /// </summary>
    public const string SandboxExpiresAt = "demo_exp";
}
