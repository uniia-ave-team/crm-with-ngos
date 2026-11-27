using Crm.Domain.Enums;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Symmetrically encrypts and decrypts typed action-token metadata using authenticated encryption (AES-256-GCM)
/// bound to both the token's <see cref="ActionTokenType"/> and normalized target email address.
/// </summary>
public interface IActionTokenProtector
{
    /// <summary>
    /// Serializes and encrypts <paramref name="metadata"/> for storage in <c>UserActionTokens.EncryptedMetadata</c>.
    /// </summary>
    /// <typeparam name="TMetadata">The typed metadata payload record.</typeparam>
    /// <param name="metadata">The metadata instance to protect.</param>
    /// <param name="tokenType">The action token category bound as authenticated associated data (AAD).</param>
    /// <param name="targetEmail">The target email address bound as AAD to prevent cross-row ciphertext substitution.</param>
    /// <returns>A versioned Base64Url-encoded authenticated ciphertext string.</returns>
    string Protect<TMetadata>(TMetadata metadata, ActionTokenType tokenType, string targetEmail);

    /// <summary>
    /// Decrypts, authenticates, and deserializes <paramref name="encryptedMetadata"/> into <typeparamref name="TMetadata"/>.
    /// </summary>
    /// <typeparam name="TMetadata">The expected metadata payload type.</typeparam>
    /// <param name="encryptedMetadata">The stored ciphertext string.</param>
    /// <param name="tokenType">The expected action token category verified via AAD.</param>
    /// <param name="targetEmail">The expected target email address verified via AAD.</param>
    /// <returns>
    /// The decrypted metadata instance, or <see langword="null"/> if the ciphertext is malformed, tampered with,
    /// bound to a different <see cref="ActionTokenType"/> or email, or cannot be deserialized.
    /// </returns>
    TMetadata? Unprotect<TMetadata>(string encryptedMetadata, ActionTokenType tokenType, string targetEmail);
}
