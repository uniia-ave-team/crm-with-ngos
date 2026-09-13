using System.Security.Claims;
using Crm.Application.Dtos.Auth;
using Crm.Application.Dtos.User;
using Crm.Domain.Entities;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a service for generating and validating JSON Web Tokens (JWT).
/// Designed to produce minimalistic tokens, relying on server-side caching for detailed permissions.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Generates a minimalistic access token containing essential user identity claims and assigned role IDs,
    /// along with its exact expiration timestamp.
    /// Permission claims and role names are intentionally omitted to support policy-based authorization securely.
    /// </summary>
    /// <param name="user">The authenticated user entity for which the token is being generated.</param>
    /// <param name="roleIds">A collection of role unique identifiers assigned to the user.</param>
    /// <returns>A <see cref="TokenResult"/> containing the JWT access token string and its expiration time.</returns>
    TokenResult GenerateAccessToken(AuthUser user, IList<Guid> roleIds);

    /// <summary>
    /// Generates a cryptographically secure random string to be used as a Refresh Token,
    /// along with its exact expiration timestamp.
    /// </summary>
    /// <returns>A <see cref="TokenResult"/> containing the base64-encoded refresh token string and its expiration time.</returns>
    TokenResult GenerateRefreshToken();

    /// <summary>
    /// Extracts claims from an expired access token.
    /// Required for validating the user's identity during the token refresh process.
    /// </summary>
    /// <param name="token">The expired JWT access token.</param>
    /// <returns>
    /// A <see cref="ClaimsPrincipal"/> containing the token's claims if validation (ignoring expiration) is successful;
    /// otherwise, <c>null</c>.
    /// </returns>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);

    /// <summary>
    /// Generates a time-limited invitation token along with its expiration time used for secure user onboarding and registration.
    /// </summary>
    /// <param name="dto">The data transfer object containing the email, organization identifier, and role identifiers for the invited user.</param>
    /// <returns>An <see cref="InvitationTokenResultDto"/> containing the generated JWT token string and its exact expiration timestamp.</returns>
    InvitationTokenResultDto GenerateInvitationToken(GenerateInvitationDto dto);

    /// <summary>
    /// Asynchronously validates a provided invitation token and extracts its embedded claims.
    /// </summary>
    /// <param name="token">The JWT invitation token to validate.</param>
    /// <returns>
    /// A task representing the asynchronous operation, containing a <see cref="ClaimsPrincipal"/> with the token's claims if validation is successful;
    /// otherwise, <c>null</c> if the token is invalid, expired, or lacks the required registration claims.
    /// </returns>
    Task<ClaimsPrincipal?> ValidateInvitationTokenAsync(string token);
}
