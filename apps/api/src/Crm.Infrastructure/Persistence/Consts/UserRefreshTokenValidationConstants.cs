namespace Crm.Infrastructure.Persistence.Consts;

/// <summary>
/// Provides persistence column-length constants for <see cref="Entities.UserRefreshToken"/> entities.
/// </summary>
public static class UserRefreshTokenValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a stored hashed refresh token string.
    /// </summary>
    public const int MaxTokenLength = 512;
}
