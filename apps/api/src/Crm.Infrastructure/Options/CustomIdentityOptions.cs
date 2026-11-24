using System.ComponentModel.DataAnnotations;

namespace Crm.Infrastructure.Options;

/// <summary>
/// Configuration options controlling ASP.NET Core Identity password complexity and user uniqueness requirements.
/// </summary>
public sealed class CustomIdentityOptions
{
    /// <summary>
    /// The configuration section name used to bind identity options.
    /// </summary>
    public const string Position = "IdentityOptions";

    /// <summary>
    /// Gets or sets the minimum required password length.
    /// </summary>
    [Range(1, 100, ErrorMessage = "RequiredLength must be between 1 and 100.")]
    public int RequiredLength { get; set; } = 8;

    /// <summary>
    /// Gets or sets a value indicating whether passwords must contain at least one numeric digit.
    /// </summary>
    public bool RequireDigit { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether passwords must contain at least one non-alphanumeric symbol.
    /// </summary>
    public bool RequireNonAlphanumeric { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether passwords must contain at least one uppercase character.
    /// </summary>
    public bool RequireUppercase { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether passwords must contain at least one lowercase character.
    /// </summary>
    public bool RequireLowercase { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether every user account must have a unique email address.
    /// </summary>
    public bool RequireUniqueEmail { get; set; } = true;
}
