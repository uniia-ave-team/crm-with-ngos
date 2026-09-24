namespace Crm.Domain.Enums;

/// <summary>
/// Represents all available system permissions.
/// </summary>
public enum AccessRight
    : byte
{
    // --- User ---
    ViewUser,
    CreateUser,
    UpdateUser,
    DisableUser,
    AssignRoleToUser,
    ViewEmergencyContact,

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
