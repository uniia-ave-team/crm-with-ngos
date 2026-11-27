using Crm.Domain.Enums;

namespace Crm.Domain.Common;

/// <summary>
/// Lean read-only projection of a <see cref="Entities.UserActionToken"/> without joining the <see cref="Entities.User"/> table
/// (used for pre-registration flows such as user invitations).
/// </summary>
/// <param name="Id">The unique identifier of the action token record.</param>
/// <param name="Type">The category of the action token.</param>
/// <param name="Email">The target user's email address.</param>
/// <param name="EncryptedMetadata">The AES-256-GCM encrypted metadata envelope.</param>
/// <param name="CreatedAt">The UTC+0 creation timestamp.</param>
/// <param name="ExpiresAt">The UTC+0 expiration timestamp.</param>
/// <param name="CreatedBy">The optional identifier of the administrator who issued the token.</param>
public sealed record ActionTokenRecord(
    Guid Id,
    ActionTokenType Type,
    string Email,
    string EncryptedMetadata,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? CreatedBy);
