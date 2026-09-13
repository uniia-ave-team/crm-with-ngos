using Microsoft.AspNetCore.Identity;

namespace Crm.Domain.Entities;

/// <summary>
/// Represents an authenticated system user handling security credentials, authentication state,
/// and session tokens. Inherits from ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/>
/// using a <see cref="Guid"/> identifier and maintains a 1-to-1 relationship with the business <see cref="User"/> profile.
/// </summary>
public class AuthUser : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets the extended business profile associated with this authentication user.
    /// </summary>
    public User UserProfile { get; set; }

    /// <summary>
    /// Gets or sets the cryptographic refresh token used for session rotation and extending access.
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the UTC expiration timestamp for the associated refresh token.
    /// </summary>
    public DateTime? RefreshTokenExpiryTime { get; set; }
}
