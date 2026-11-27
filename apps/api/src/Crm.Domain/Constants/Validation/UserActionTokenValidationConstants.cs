namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Validation and persistence length constants for <see cref="Entities.UserActionToken"/> entities.
/// </summary>
public static class UserActionTokenValidationConstants
{
    /// <summary>
    /// The fixed character length of an uppercase hex-encoded SHA-256 token hash.
    /// </summary>
    public const int TokenHashLength = 64;

    /// <summary>
    /// The maximum allowed length for the target user email address stored on an action token.
    /// </summary>
    public const int MaxTargetEmailLength = AuthUserValidationConstants.MaxEmailLength;

    /// <summary>
    /// The maximum allowed length for the symmetrically encrypted action-token metadata ciphertext.
    /// </summary>
    public const int MaxEncryptedMetadataLength = 4096;
}
