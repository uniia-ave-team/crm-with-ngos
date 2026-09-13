using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

public class JwtOptions
{
    /// <summary>
    /// The configuration section name used to bind these options from the settings file.
    /// </summary>
    public const string Position = "JwtOptions";

    [Required(ErrorMessage = "JWT Secret is required")]
    [MinLength(16, ErrorMessage = "JWT Secret must be at least 16 characters long")]
    public string Secret { get; set; }

    [Required]
    public string Issuer { get; set; }

    [Required]
    public string Audience { get; set; }

    /// <summary>
    /// Gets or sets expiry time for Access Token in minutes (e.g., 15-60 minutes).
    /// </summary>
    public int AccessTokenExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets expiry time for Refresh Token in days (e.g., 7 days).
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets expiry time for Invitation Token in hours (e.g., 48 hours).
    /// </summary>
    [Range(1, 8760, ErrorMessage = "InvitationTokenExpiryHours must be between 1 and 8760.")]
    public int InvitationTokenExpiryHours { get; set; } = 48;
}
