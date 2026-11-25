using Crm.Domain.Entities.Base;
using Crm.Domain.Enums;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents a universal, server-stored, single-use opaque action token (such as a user invitation or password reset link).
/// Only the SHA-256 hash of the raw opaque token is persisted; token-type-specific metadata is stored as authenticated
/// symmetrically encrypted ciphertext in <see cref="EncryptedMetadata"/>.
/// </summary>
public class UserActionToken : BaseEntity, ICreatableEntity
{
    /// <summary>
    /// Gets or sets the functional category of this action token.
    /// </summary>
    public ActionTokenType Type { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 hex digest of the raw opaque token handed to the client.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the target email address associated with this token.
    /// </summary>
    public string TargetEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the uppercase normalized target email address for case-insensitive lookups and bulk revocation.
    /// </summary>
    public string NormalizedTargetEmail { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional identifier of the existing user targeted by this token (set for existing-user flows such as password resets).
    /// </summary>
    public Guid? TargetUserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the targeted <see cref="User"/> profile, when applicable.
    /// </summary>
    public User? TargetUser { get; set; }

    /// <summary>
    /// Gets or sets the symmetrically encrypted (AES-256-GCM) token-type-specific metadata envelope.
    /// </summary>
    public string EncryptedMetadata { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this token expires.
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC+0 timestamp when this token was issued.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the optional identifier of the user who issued this token.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the navigation property to the user who issued this token.
    /// </summary>
    public User? CreatedByUser { get; set; }
}
