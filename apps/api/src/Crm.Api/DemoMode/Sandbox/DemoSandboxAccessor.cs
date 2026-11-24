namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Reads the current sandbox from <see cref="HttpContext.Items"/>, where <see cref="DemoModeMiddleware"/> puts it.
/// The HTTP context flows into service scopes created during the request, so they see the same sandbox.
/// </summary>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
public sealed class DemoSandboxAccessor(IHttpContextAccessor httpContextAccessor) : IDemoSandboxAccessor
{
    private static readonly object ItemKey = new();
    private static readonly AsyncLocal<bool> SharedDatabaseScope = new();

    /// <inheritdoc />
    public bool IsHttpRequest => httpContextAccessor.HttpContext is not null;

    /// <inheritdoc />
    public DemoSandbox? Current => SharedDatabaseScope.Value ? null : GetSandbox(httpContextAccessor.HttpContext);

    /// <summary>
    /// Makes the code inside the returned scope work with the shared demo database, even during a request of a
    /// visitor with a sandbox: <see cref="Current"/> is <c>null</c> there. Inside a request the shared database
    /// stays read-only (<see cref="DemoDbWriteGuardInterceptor"/>).
    /// </summary>
    /// <returns>The scope; disposing it returns to the request's sandbox.</returns>
    public static IDisposable UseSharedDatabase()
    {
        bool previous = SharedDatabaseScope.Value;
        SharedDatabaseScope.Value = true;

        return new SharedDatabaseScopeHandle(previous);
    }

    /// <summary>
    /// Reads the sandbox assigned to a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>The sandbox, or <c>null</c>.</returns>
    public static DemoSandbox? GetSandbox(HttpContext? context) =>
        context is not null && context.Items.TryGetValue(ItemKey, out object? value) ? value as DemoSandbox : null;

    /// <summary>
    /// Assigns a sandbox to a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="sandbox">The sandbox.</param>
    public static void SetSandbox(HttpContext context, DemoSandbox sandbox)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Items[ItemKey] = sandbox;
    }

    private sealed class SharedDatabaseScopeHandle(bool previous) : IDisposable
    {
        public void Dispose() => SharedDatabaseScope.Value = previous;
    }
}
