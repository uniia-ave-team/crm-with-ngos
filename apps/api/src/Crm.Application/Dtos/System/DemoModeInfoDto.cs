namespace Crm.Application.Dtos.System;

/// <summary>
/// What the web client needs to know about the public demo mode.
/// </summary>
/// <param name="Accounts">The demo accounts offered on the sign-in page.</param>
/// <param name="SessionIdleMinutes">Minutes of inactivity after which a visitor's demo data is deleted.</param>
/// <param name="SessionMaxMinutes">The maximum lifetime of a visitor's demo data in minutes.</param>
public record DemoModeInfoDto(
    IReadOnlyList<DemoAccountDto> Accounts,
    int SessionIdleMinutes,
    int SessionMaxMinutes);
