namespace Crm.Application.Enums;

/// <summary>
/// Specifies the types of user management operations.
/// </summary>
public enum UserOperation
    : byte
{
    Create = 0,
    Update = 1,
    Delete = 2,
    AssignRole = 3,
    RemoveRole = 4,
    UpdateRefreshToken = 5,
    ChangePassword = 6,
}
