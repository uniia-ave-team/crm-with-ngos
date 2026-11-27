namespace Crm.Application.Dtos.Users;

/// <summary>
/// Represents the target user's email address and the underlying ASP.NET Identity password reset token
/// returned in a single identity lookup.
/// </summary>
/// <param name="Email">The target user's email address.</param>
/// <param name="ResetToken">The generated ASP.NET Identity password reset token.</param>
public sealed record IdentityPasswordResetTokenResult(
    string Email,
    string ResetToken);
