namespace Crm.Domain.Enums;

/// <summary>
/// Represents all available system permissions.
/// </summary>
public enum AccessRight
    : byte
{
    // --- User ---
    ViewUser = 0,
    CreateUser = 1,
    UpdateUser = 2,
    DisableUser = 3,
    AssignRoleToUser = 4,
    ViewEmergencyContact = 5,

    // --- Role ---
    ViewRole = 6,
    CreateRole = 7,
    UpdateRole = 8,
    DeleteRole = 9,
    ManageRolePermissions = 10,

    // --- Ngo ---
    ViewNgo = 11,
    CreateNgo = 12,
    UpdateNgo = 13,
}
