namespace Crm.Api.Dtos;

/// <summary>
/// Request to log out the currently authenticated user by revoking their refresh token.
/// </summary>
/// <param name="RefreshToken">The refresh token to revoke for the current device session.</param>
public record LogoutRequest(
    string RefreshToken);
