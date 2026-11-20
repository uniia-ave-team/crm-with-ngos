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
}
