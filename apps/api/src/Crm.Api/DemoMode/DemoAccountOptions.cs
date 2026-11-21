using System.ComponentModel.DataAnnotations;
using Crm.Domain.Consts;

namespace Crm.Api.DemoMode;

/// <summary>
/// Describes one public demo account: the label shown to visitors and the credentials used to sign in.
/// </summary>
/// <remarks>
/// Demo credentials are public by design: every visitor works in a private throw-away copy of the demo database.
/// </remarks>
public sealed class DemoAccountOptions
{
    /// <summary>
    /// The name of the demo role with broad day-to-day permissions but no system or role administration.
    /// </summary>
    public const string CoordinatorRole = "Координатор";

    /// <summary>
    /// The name of the demo role with read-mostly permissions.
    /// </summary>
    public const string VolunteerRole = "Волонтер";

    /// <summary>
    /// Gets the built-in demo accounts used when none are configured: an administrator, a coordinator and a volunteer.
    /// A fresh list is returned on every call, so callers may adjust the instances freely.
    /// </summary>
    public static IReadOnlyList<DemoAccountOptions> Defaults =>
    [
        new()
        {
            Name = "Адміністратор",
            Email = "admin@demo.example.org",
            Password = "Demo-Admin-2026!",
            Role = RoleConsts.Admin,
        },
        new()
        {
            Name = "Координатор",
            Email = "coordinator@demo.example.org",
            Password = "Demo-Coordinator-2026!",
            Role = CoordinatorRole,
        },
        new()
        {
            Name = "Волонтер",
            Email = "volunteer@demo.example.org",
            Password = "Demo-Volunteer-2026!",
            Role = VolunteerRole,
        },
    ];

    /// <summary>
    /// Gets or sets the label of the account shown to demo visitors (for example on the sign-in page).
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the e-mail address used to sign in.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the password used to sign in. It must satisfy the configured Identity password policy.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the role assigned to the account.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(256)]
    public string Role { get; set; } = string.Empty;
}
