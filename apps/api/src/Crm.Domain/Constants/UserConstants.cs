namespace Crm.Domain.Constants;

/// <summary>
/// Provides canonical user property, sort-alias, and Identity field constants
/// shared across Domain validation, Application queries, and Infrastructure ordering.
/// </summary>
public static class UserConstants
{
    /// <summary>
    /// The property alias used for composite full-name sorting (LastName -> FirstName -> Patronymic).
    /// </summary>
    public const string FullName = "FullName";

    /// <summary>
    /// The canonical property name representing a user's email address (exposed via <c>AuthUser</c> and <c>UserDto</c>).
    /// </summary>
    public const string Email = "Email";

    /// <summary>
    /// The canonical DTO property name representing a user's resolved avatar URL.
    /// </summary>
    public const string AvatarUrl = "AvatarUrl";

    /// <summary>
    /// The canonical DTO property name representing a user's assigned roles collection.
    /// </summary>
    public const string Roles = "Roles";

    /// <summary>
    /// The canonical DTO property name representing a user's custom fields collection.
    /// </summary>
    public const string CustomFields = "CustomFields";

    /// <summary>
    /// The container prefix used in <c>OrderBy</c> expressions targeting user custom fields
    /// (for example, <c>customFields.city</c> or <c>customFields['birth_date']</c>).
    /// </summary>
    public const string CustomFieldsSortPrefix = "customFields";

    /// <summary>
    /// ASP.NET Core Identity <c>IdentityUser&lt;Guid&gt;</c> property names reserved against custom field key collisions.
    /// </summary>
    public static readonly IReadOnlyList<string> IdentityReservedKeys =
    [
        "UserName",
        "NormalizedUserName",
        "NormalizedEmail",
        "EmailConfirmed",
        "PasswordHash",
        "SecurityStamp",
        "ConcurrencyStamp",
        "PhoneNumberConfirmed",
        "TwoFactorEnabled",
        "LockoutEnd",
        "LockoutEnabled",
        "AccessFailedCount",
    ];
}
