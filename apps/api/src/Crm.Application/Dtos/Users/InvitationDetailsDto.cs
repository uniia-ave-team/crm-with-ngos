namespace Crm.Application.Dtos.Users;

/// <summary>
/// Data transfer object containing the details extracted from a validated invitation token.
/// </summary>
/// <param name="Email">The invited user's email address.</param>
/// <param name="NgoId">The unique identifier of the NGO organization.</param>
/// <param name="RoleIds">The collection of role identifiers assigned to the invited user.</param>
public record InvitationDetailsDto(
    string Email,
    Guid NgoId,
    IReadOnlyList<Guid> RoleIds);
