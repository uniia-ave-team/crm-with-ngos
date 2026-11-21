using System.Security.Claims;
using Crm.Application.Dtos.User;

namespace Crm.Application.Interfaces;

/// <summary>
/// Adds extra claims to the access tokens issued by <see cref="ITokenService"/>.
/// </summary>
public interface IAccessTokenClaimsProvider
{
    /// <summary>
    /// Returns the claims to add to the access token of a user.
    /// </summary>
    /// <param name="user">The user the token is issued for.</param>
    /// <returns>The extra claims; empty when there are none.</returns>
    IEnumerable<Claim> GetClaims(UserBasicDto user);
}
