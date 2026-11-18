namespace Crm.Api.DemoMode;

/// <summary>
/// Provides access to the current Demo Mode activation state.
/// </summary>
public interface IDemoModeState
{
    /// <summary>
    /// Gets a value indicating whether Demo Mode is currently enabled.
    /// </summary>
    bool IsActive { get; }
}
