namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object containing target account details extracted from a validated password reset token.
/// Allows the client UI to display which user account's password is being reset before submission.
/// </summary>
/// <param name="Email">The email address of the user whose password will be reset.</param>
/// <param name="FirstName">The first name of the user whose password will be reset.</param>
/// <param name="LastName">The last name of the user whose password will be reset.</param>
/// <param name="AvatarUrl">The resolved URL or API endpoint to access the target user's avatar image, if set.</param>
/// <param name="ExpiresAt">The UTC+0 timestamp when the reset token expires.</param>
public record PasswordResetDetailsDto(
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    DateTime ExpiresAt);
