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
    ViewCustomFields = 6,

    // --- Role ---
    ViewRole = 7,
    CreateRole = 8,
    UpdateRole = 9,
    DeleteRole = 10,
    ManageRolePermissions = 11,

    // --- Ngo ---
    ViewNgo = 12,
    CreateNgo = 13,
    UpdateNgo = 14,
}
