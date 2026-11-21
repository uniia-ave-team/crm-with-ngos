namespace Crm.Application.Dtos.System;

/// <summary>
/// A demo account offered on the sign-in page. Its credentials are public by design.
/// </summary>
/// <param name="Name">The label of the account, for example its role.</param>
/// <param name="Email">The e-mail used to sign in.</param>
/// <param name="Password">The password used to sign in.</param>
public record DemoAccountDto(string Name, string Email, string Password);
