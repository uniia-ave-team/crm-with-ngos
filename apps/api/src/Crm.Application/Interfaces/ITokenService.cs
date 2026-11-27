using System.Security.Claims;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.Users;
using Crm.Domain.Enums;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a service for generating and validating JWT access tokens, cryptographic refresh tokens,
/// and short opaque action tokens (such as invitations and password resets).
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a minimalistic access token containing essential user identity claims and assigned role IDs,
    /// along with its exact expiration timestamp.
    /// Permission claims and role names are intentionally omitted to support policy-based authorization securely.
    /// </summary>
    /// <param name="user">The basic user data transfer object containing the unique identifier and email for whom the token is being generated.</param>
    /// <param name="roleIds">A collection of role unique identifiers assigned to the user.</param>
    /// <returns>A <see cref="TokenResult"/> containing the JWT access token string and its expiration time.</returns>
    TokenResult GenerateAccessToken(UserBasicDto user, IList<Guid> roleIds);

    /// <summary>
    /// Generates a cryptographically secure random string to be used as a Refresh Token,
    /// along with its exact expiration timestamp.
    /// </summary>
    /// <returns>A <see cref="TokenResult"/> containing the Base64Url-encoded refresh token string and its expiration time.</returns>
    TokenResult GenerateRefreshToken();

    /// <summary>
    /// Extracts claims from an expired access token.
    /// Required for validating the user's identity during the token refresh process.
    /// </summary>
    /// <param name="token">The expired JWT access token.</param>
    /// <returns>
    /// A <see cref="ClaimsPrincipal"/> containing the token's claims if validation (ignoring expiration) is successful;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

    /// <summary>
    /// Generates a short, URL-safe, cryptographically random opaque token along with its configured UTC expiration timestamp
    /// for the specified <see cref="ActionTokenType"/>.
    /// </summary>
    /// <param name="tokenType">The functional category of action token to generate.</param>
    /// <returns>A <see cref="TokenResultDto"/> containing the opaque token string and its UTC+0 expiration timestamp.</returns>
    TokenResultDto GenerateActionToken(ActionTokenType tokenType);
}
