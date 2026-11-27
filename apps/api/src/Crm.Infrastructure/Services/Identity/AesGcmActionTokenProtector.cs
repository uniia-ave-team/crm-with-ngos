using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crm.Domain.Enums;
using Crm.Infrastructure.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Services.Identity;

/// <summary>
/// Encrypts and authenticates <see cref="Domain.Entities.UserActionToken.EncryptedMetadata"/> payloads
/// using AES-256-GCM with an HKDF-SHA256 key derived from <see cref="JwtOptions.Secret"/> and
/// <see cref="ActionTokenType"/> + normalized target email bound as associated authenticated data (AAD).
/// </summary>
public sealed class AesGcmActionTokenProtector : IActionTokenProtector
{
    private const string EnvelopePrefix = "v1.";
    private const int KeySizeBytes = 32;
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;

    private static readonly byte[] KeyDerivationInfo = "crm:user-action-token-metadata:aes-256-gcm:v1"u8.ToArray();
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly byte[] _encryptionKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="AesGcmActionTokenProtector"/> class.
    /// </summary>
    /// <param name="jwtOptions">The JWT configuration options providing the master secret for HKDF key derivation.</param>
    public AesGcmActionTokenProtector(IOptions<JwtOptions> jwtOptions)
    {
        byte[] inputKeyMaterial = Encoding.UTF8.GetBytes(jwtOptions.Value.Secret);
        _encryptionKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            inputKeyMaterial,
            KeySizeBytes,
            info: KeyDerivationInfo);
    }

    /// <inheritdoc />
    public string Protect<TMetadata>(TMetadata metadata, ActionTokenType tokenType, string targetEmail)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetEmail);

        byte[] plaintext = JsonSerializer.SerializeToUtf8Bytes(metadata, SerializerOptions);
        byte[] envelope = new byte[NonceSizeBytes + TagSizeBytes + plaintext.Length];

        Span<byte> nonce = envelope.AsSpan(0, NonceSizeBytes);
        Span<byte> tag = envelope.AsSpan(NonceSizeBytes, TagSizeBytes);
        Span<byte> ciphertext = envelope.AsSpan(NonceSizeBytes + TagSizeBytes);

        RandomNumberGenerator.Fill(nonce);

        byte[] associatedData = BuildAssociatedData(tokenType, targetEmail);

        using var aesGcm = new AesGcm(_encryptionKey, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return string.Concat(EnvelopePrefix, WebEncoders.Base64UrlEncode(envelope));
    }

    /// <inheritdoc />
    public TMetadata? Unprotect<TMetadata>(string encryptedMetadata, ActionTokenType tokenType, string targetEmail)
    {
        if (string.IsNullOrWhiteSpace(encryptedMetadata)
            || string.IsNullOrWhiteSpace(targetEmail)
            || !encryptedMetadata.StartsWith(EnvelopePrefix, StringComparison.Ordinal))
        {
            return default;
        }

        try
        {
            byte[] envelope = WebEncoders.Base64UrlDecode(encryptedMetadata[EnvelopePrefix.Length..]);
            if (envelope.Length <= NonceSizeBytes + TagSizeBytes)
            {
                return default;
            }

            ReadOnlySpan<byte> nonce = envelope.AsSpan(0, NonceSizeBytes);
            ReadOnlySpan<byte> tag = envelope.AsSpan(NonceSizeBytes, TagSizeBytes);
            ReadOnlySpan<byte> ciphertext = envelope.AsSpan(NonceSizeBytes + TagSizeBytes);
            byte[] plaintext = new byte[ciphertext.Length];

            byte[] associatedData = BuildAssociatedData(tokenType, targetEmail);

            using var aesGcm = new AesGcm(_encryptionKey, TagSizeBytes);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

            return JsonSerializer.Deserialize<TMetadata>(plaintext, SerializerOptions);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return default;
        }
    }

    private static byte[] BuildAssociatedData(ActionTokenType tokenType, string targetEmail)
        => Encoding.UTF8.GetBytes($"{(byte)tokenType}:{targetEmail.Trim().ToUpperInvariant()}");
}
