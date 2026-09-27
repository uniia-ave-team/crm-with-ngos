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
    DeleteUser = 4,
    AssignRoleToUser = 5,
    ViewEmergencyContact = 6,
    ViewCustomFields = 7,

    // --- Role ---
    ViewRole = 8,
    CreateRole = 9,
    UpdateRole = 10,
    DeleteRole = 11,
    ManageRolePermissions = 12,

    // --- Ngo ---
    ViewNgo = 13,
    CreateNgo = 14,
    UpdateNgo = 15,

    // --- Login Page Images ---
    ViewLoginPageImages = 16,
    CreateLoginPageImages = 17,
    DeleteLoginPageImages = 18,
}
