namespace Crm.Infrastructure.Consts;

/// <summary>
/// Provides constant values related to cryptographic token generation and background token cleanup.
/// </summary>
public static class TokenConstants
{
    /// <summary>
    /// The length of the cryptographically secure random byte array used to generate refresh tokens.
    /// 32 bytes provides 256 bits of entropy, which is the industry standard for secure tokens.
    /// </summary>
    public const int RefreshTokenBytesLength = 32;

    /// <summary>
    /// The length of the cryptographically secure random byte array used to generate short opaque action tokens
    /// (invitations and password resets). 18 bytes produces a clean 24-character Base64Url string with 144 bits of entropy.
    /// </summary>
    public const int ActionTokenBytesLength = 18;

    /// <summary>
    /// The interval in hours at which the expired tokens cleanup process runs.
    /// </summary>
    public const int CleanupIntervalHours = 24;
}
