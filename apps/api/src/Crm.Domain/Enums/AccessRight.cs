namespace Crm.Domain.Enums;

/// <summary>
/// Represents all available system permissions.
/// </summary>
public enum AccessRight
{
    // --- User ---
    ViewUser,
    CreateUser,
    UpdateUser,
    DisableUser,
    AssignRoleToUser,

    // --- Role ---
    ViewRole,
    CreateRole,
    UpdateRole,
    DeleteRole,
    ManageRolePermissions,

    // --- Ngo ---
    ViewNgo,
    CreateNgo,
    UpdateNgo,
}
