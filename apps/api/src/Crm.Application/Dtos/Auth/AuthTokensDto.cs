namespace Crm.Application.Dtos.Auth;

/// <summary>
/// Represents the authentication tokens returned to the client.
/// </summary>
/// <param name="AccessToken">The JWT access token used for API authorization.</param>
/// <param name="RefreshToken">The secure refresh token used to obtain new access tokens.</param>
public record AuthTokensDto(
    string AccessToken,
    string RefreshToken);
