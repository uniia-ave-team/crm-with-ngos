using Crm.Domain.Enums;

namespace Crm.Domain.Common;

/// <summary>
/// Lean read-only projection of a <see cref="Entities.UserActionToken"/> record
/// selecting only the columns required by validation, consumption, and listing flows
/// (avoiding full <see cref="Entities.User"/> entity hydration).
/// </summary>
/// <param name="Id">The unique identifier of the action token record.</param>
/// <param name="Type">The category of the action token.</param>
/// <param name="TargetEmail">The target user's email address.</param>
/// <param name="TargetUserId">The optional identifier of the targeted user.</param>
/// <param name="TargetFirstName">The targeted user's first name, when applicable.</param>
/// <param name="TargetLastName">The targeted user's last name, when applicable.</param>
/// <param name="TargetAvatar">The targeted user's stored avatar file key, when applicable.</param>
/// <param name="EncryptedMetadata">The AES-256-GCM encrypted metadata envelope, populated only when requested.</param>
/// <param name="CreatedAt">The UTC+0 creation timestamp.</param>
/// <param name="ExpiresAt">The UTC+0 expiration timestamp.</param>
/// <param name="CreatedBy">The optional identifier of the administrator who issued the token.</param>
public sealed record UserActionTokenRecord(
    Guid Id,
    ActionTokenType Type,
    string TargetEmail,
    Guid? TargetUserId,
    string? TargetFirstName,
    string? TargetLastName,
    string? TargetAvatar,
    string? EncryptedMetadata,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? CreatedBy);
