namespace Crm.Api.DemoMode;

/// <summary>
/// What a session endpoint does, from the point of view of demo mode.
/// </summary>
public enum DemoSessionEndpointKind
{
    /// <summary>
    /// The endpoint signs a user in: demo mode starts a sandbox for the visitor.
    /// </summary>
    SignIn = 0,

    /// <summary>
    /// The endpoint exchanges tokens: demo mode finds the sandbox named in the access token of the request body.
    /// </summary>
    Refresh = 1,
}
