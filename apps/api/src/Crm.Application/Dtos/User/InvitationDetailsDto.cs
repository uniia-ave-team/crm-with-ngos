namespace Crm.Application.Dtos.User;

/// <summary>
/// Data transfer object containing the details extracted from a validated invitation token.
/// </summary>
public record InvitationDetailsDto(
    string Email,
    Guid NgoId,
    Guid RoleId);
