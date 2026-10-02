namespace Crm.Application.Dtos.Auth;

/// <summary>
/// Represents a generated token along with its expiration time.
/// </summary>
/// <param name="Token">The generated token string.</param>
/// <param name="ExpiresAt">The exact UTC time when the token expires.</param>
public record TokenResult(
    string Token,
    DateTime ExpiresAt);
