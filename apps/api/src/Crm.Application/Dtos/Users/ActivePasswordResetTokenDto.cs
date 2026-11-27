namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object representing an active (unexpired) password reset token issued for a user.
/// </summary>
/// <param name="Id">The unique identifier of the password reset token record (used for revocation).</param>
/// <param name="UserId">The unique identifier of the target user.</param>
/// <param name="Email">The target user's email address.</param>
/// <param name="FirstName">The target user's first name.</param>
/// <param name="LastName">The target user's last name.</param>
/// <param name="AvatarUrl">The resolved URL or API endpoint to access the target user's avatar image, if set.</param>
/// <param name="CreatedAt">The UTC+0 timestamp when the password reset token was created.</param>
/// <param name="ExpiresAt">The UTC+0 timestamp when the password reset token expires.</param>
/// <param name="CreatedBy">The optional identifier of the administrator who generated the token.</param>
public record ActivePasswordResetTokenDto(
    Guid Id,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string? AvatarUrl,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? CreatedBy);
