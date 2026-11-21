namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Gives access to the sandbox the current HTTP request works in.
/// </summary>
public interface IDemoSandboxAccessor
{
    /// <summary>
    /// Gets a value indicating whether the code runs inside an HTTP request
    /// (as opposed to startup or a background service).
    /// </summary>
    bool IsHttpRequest { get; }

    /// <summary>
    /// Gets the sandbox of the current request, or <c>null</c> when the request has none
    /// (anonymous requests, startup, background services).
    /// </summary>
    DemoSandbox? Current { get; }
}
