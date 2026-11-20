namespace Crm.Application.Dtos.System;

/// <summary>
/// What the web client needs to know about the public demo mode.
/// </summary>
/// <param name="Accounts">The demo accounts offered on the sign-in page.</param>
/// <param name="SessionMinutes">
/// How long a visitor's demo data lives, in minutes, counted from the first sign-in. The exact moment
/// is in the <c>demo_exp</c> claim of the access token.
/// </param>
public record DemoModeInfoDto(
    IReadOnlyList<DemoAccountDto> Accounts,
    int SessionMinutes);
