namespace Crm.Domain.Consts;

/// <summary>
/// Provides constant values related to token generation and security across the application.
/// </summary>
public static class TokenConstants
{
    /// <summary>
    /// The length of the cryptographically secure random byte array used to generate refresh tokens.
    /// 32 bytes provides 256 bits of entropy, which is the industry standard for secure tokens.
    /// </summary>
    public const int RefreshTokenBytesLength = 32;

    /// <summary>
    /// The interval in hours at which the expired tokens cleanup process runs.
    /// </summary>
    public const int CleanupIntervalHours = 24;
}
