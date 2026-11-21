using Crm.Application.Dtos.System;
using Microsoft.Extensions.Options;

namespace Crm.Api.DemoMode;

/// <summary>
/// Builds the demo mode details from the configuration.
/// </summary>
/// <param name="demoModeOptions">The demo mode options.</param>
public sealed class DemoModeInfoProvider(IOptions<DemoModeOptions> demoModeOptions) : IDemoModeInfoProvider
{
    private readonly DemoModeInfoDto? _info = Build(demoModeOptions.Value);

    /// <inheritdoc />
    public DemoModeInfoDto? GetInfo() => _info;

    private static DemoModeInfoDto? Build(DemoModeOptions options) =>
        options.Enabled
            ? new DemoModeInfoDto(
                [.. options.GetEffectiveAccounts().Select(a => new DemoAccountDto(a.Name, a.Email, a.Password))],
                options.Sandbox.LifetimeMinutes)
            : null;
}
