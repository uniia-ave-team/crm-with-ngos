namespace Crm.Infrastructure.Entities;

/// <summary>
/// Represents a refresh token used to maintain multiple concurrent user sessions and issue new access tokens.
/// </summary>
public class UserRefreshToken
{
    /// <summary>
    /// Gets or sets the unique identifier of the refresh token record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the unique identifier of the user who owns this token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the cryptographic refresh token string value.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time in UTC when the refresh token expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the navigation property for the user associated with this refresh token.
    /// </summary>
    public virtual AuthUser User { get; set; }
}
