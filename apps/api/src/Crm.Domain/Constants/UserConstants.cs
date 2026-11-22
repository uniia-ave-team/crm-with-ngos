namespace Crm.Domain.Constants;

/// <summary>
/// Provides general constants related to the <see cref="Entities.User"/> entity and query operations.
/// </summary>
public static class UserConstants
{
    /// <summary>
    /// The property alias used for composite full-name sorting (LastName -> FirstName -> Patronymic).
    /// </summary>
    public const string FullName = "FullName";
}
