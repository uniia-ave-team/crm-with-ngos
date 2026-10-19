namespace Crm.Application.Dtos.User;

/// <summary>
/// Data transfer object containing the generated token and its expiration time.
/// </summary>
public record TokenResultDto(
    string Token,
    DateTime ExpiresAt);
