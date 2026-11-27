namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object representing an active (unexpired) user invitation token.
/// </summary>
/// <param name="Id">The unique identifier of the invitation token record (used for revocation).</param>
/// <param name="Email">The invited user's email address.</param>
/// <param name="RoleIds">The role identifiers that will be assigned upon registration.</param>
/// <param name="CreatedAt">The UTC+0 timestamp when the invitation was created.</param>
/// <param name="ExpiresAt">The UTC+0 timestamp when the invitation expires.</param>
/// <param name="CreatedBy">The optional identifier of the administrator who created the invitation.</param>
public record ActiveInvitationDto(
    Guid Id,
    string Email,
    IReadOnlyList<Guid> RoleIds,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    Guid? CreatedBy);
