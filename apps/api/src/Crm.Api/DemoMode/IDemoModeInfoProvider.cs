using Crm.Application.Dtos.System;

namespace Crm.Api.DemoMode;

/// <summary>
/// Tells the web client whether the API runs in demo mode and how to sign in.
/// </summary>
public interface IDemoModeInfoProvider
{
    /// <summary>
    /// Returns the demo mode details, or <c>null</c> when demo mode is off.
    /// </summary>
    /// <returns>The demo mode details or <c>null</c>.</returns>
    DemoModeInfoDto? GetInfo();
}
