namespace Crm.Application.Dtos.Users;

/// <summary>
/// Typed metadata payload encrypted inside a <see cref="Domain.Entities.UserActionToken"/>
/// of type <see cref="Domain.Enums.ActionTokenType.PasswordReset"/>.
/// </summary>
/// <param name="IdentityResetToken">The underlying ASP.NET Identity password reset token bound to the user's security stamp.</param>
public sealed record PasswordResetTokenMetadata(
    string IdentityResetToken);
