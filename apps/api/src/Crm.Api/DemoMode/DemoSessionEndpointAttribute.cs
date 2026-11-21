namespace Crm.Api.DemoMode;

/// <summary>
/// Marks the endpoints that start or continue a session, so that demo mode can attach a sandbox
/// to these anonymous requests. Has no effect when demo mode is off.
/// </summary>
/// <param name="kind">What the endpoint does with the session.</param>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DemoSessionEndpointAttribute(DemoSessionEndpointKind kind) : Attribute
{
    /// <summary>
    /// Gets what the endpoint does with the session.
    /// </summary>
    public DemoSessionEndpointKind Kind { get; } = kind;
}
