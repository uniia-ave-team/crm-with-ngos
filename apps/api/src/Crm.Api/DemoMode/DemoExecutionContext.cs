namespace Crm.Api.DemoMode;

/// <summary>
/// Default <see cref="AsyncLocal{T}"/>-backed implementation of <see cref="IDemoExecutionContext"/>.
/// </summary>
public sealed class DemoExecutionContext : IDemoExecutionContext
{
    private readonly AsyncLocal<DemoExecutionMode> _mode = new();

    /// <inheritdoc />
    public DemoExecutionMode Mode => _mode.Value;

    /// <inheritdoc />
    public bool IsDryRunActive => _mode.Value == DemoExecutionMode.DryRun;

    /// <inheritdoc />
    public bool IsSessionAuthActive => _mode.Value == DemoExecutionMode.SessionAuth;

    /// <inheritdoc />
    public bool IsExplicitlyAllowed => _mode.Value == DemoExecutionMode.ExplicitlyAllowed;

    /// <inheritdoc />
    public IDisposable EnterScope(DemoExecutionMode mode)
    {
        var previous = _mode.Value;
        _mode.Value = mode;
        return new ScopeReset(_mode, previous);
    }

    /// <inheritdoc />
    public IDisposable EnterScope(bool shouldDryRun)
        => EnterScope(shouldDryRun ? DemoExecutionMode.DryRun : DemoExecutionMode.ExplicitlyAllowed);

    private sealed class ScopeReset(AsyncLocal<DemoExecutionMode> slot, DemoExecutionMode previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            slot.Value = previous;
            _disposed = true;
        }
    }
}
