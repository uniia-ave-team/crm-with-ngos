namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object containing the user identifier and the generated password reset token.
/// </summary>
public record PasswordResetTokenDto(
    Guid UserId,
    string PasswordResetToken);
