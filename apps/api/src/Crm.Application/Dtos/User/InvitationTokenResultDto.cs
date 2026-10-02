namespace Crm.Application.Dtos.User;

/// <summary>
/// Data transfer object containing the generated invitation token and its expiration time.
/// </summary>
public record InvitationTokenResultDto(
    string Token,
    DateTime ExpiresAt);
