namespace Crm.Application.Dtos.User;

/// <summary>
/// Data transfer object containing parameters required to generate a user invitation token.
/// </summary>
public record GenerateInvitationDto(
    string Email,
    Guid NgoId,
    IEnumerable<Guid> RoleIds);
