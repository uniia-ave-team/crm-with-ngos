using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Provides the startup-configured Demo Mode activation state via <see cref="IOptions{TOptions}"/>.
/// </summary>
/// <param name="demoModeOptions">The options providing <see cref="DemoModeOptions"/> values.</param>
public sealed class DemoModeState(IOptions<DemoModeOptions> demoModeOptions) : IDemoModeState
{
    /// <inheritdoc />
    public bool IsActive => demoModeOptions.Value.Enabled;
}
