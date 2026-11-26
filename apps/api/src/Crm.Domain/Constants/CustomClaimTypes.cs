namespace Crm.Domain.Constants;

/// <summary>
/// Constants representing custom claim types used within the application's JWT tokens and authorization policies.
/// </summary>
public static class CustomClaimTypes
{
    /// <summary>
    /// The subject identifier claim (<c>sub</c>).
    /// </summary>
    public const string Sub = "sub";

    /// <summary>
    /// The claim type storing role permissions.
    /// </summary>
    public const string Permission = "Permission";

    /// <summary>
    /// The claim type distinguishing invitation token kinds.
    /// </summary>
    public const string InviteType = "invite_type";

    /// <summary>
    /// The claim type storing the associated NGO identifier.
    /// </summary>
    public const string NgoId = "ngo_id";

    /// <summary>
    /// The claim type storing assigned role identifiers.
    /// </summary>
    public const string RoleId = "role_id";

    /// <summary>
    /// The claim value marking a registration invitation token.
    /// </summary>
    public const string RegistrationInviteValue = "registration";

    /// <summary>
    /// The claim type storing the embedded ASP.NET Identity password reset token.
    /// </summary>
    public const string PasswordResetToken = "password_reset";

    /// <summary>
    /// The claim type distinguishing the purpose of a generated JWT token.
    /// </summary>
    public const string TokenType = "token_type";

    /// <summary>
    /// The claim value marking an authentication access token.
    /// </summary>
    public const string AccessTokenValue = "access";

    /// <summary>
    /// The claim value marking a user invitation token.
    /// </summary>
    public const string InvitationTokenValue = "invitation";

    /// <summary>
    /// The claim value marking a password reset token.
    /// </summary>
    public const string PasswordResetTokenValue = "password_reset";

    /// <summary>
    /// The high-precision UTC ticks timestamp (<see cref="DateTimeOffset.UtcTicks"/>) recorded when an access token was issued,
    /// enabling sub-second comparisons against timestamped token revocation entries.
    /// </summary>
    public const string IssuedAtTicks = "iat_ticks";
}
