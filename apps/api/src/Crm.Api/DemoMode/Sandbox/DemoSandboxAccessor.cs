namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Reads the current sandbox from <see cref="HttpContext.Items"/>, where <see cref="DemoModeMiddleware"/> puts it.
/// The HTTP context flows into service scopes created during the request, so they see the same sandbox.
/// </summary>
/// <param name="httpContextAccessor">The HTTP context accessor.</param>
public sealed class DemoSandboxAccessor(IHttpContextAccessor httpContextAccessor) : IDemoSandboxAccessor
{
    private static readonly object ItemKey = new();

    /// <inheritdoc />
    public bool IsHttpRequest => httpContextAccessor.HttpContext is not null;

    /// <inheritdoc />
    public DemoSandbox? Current => GetSandbox(httpContextAccessor.HttpContext);

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
}
