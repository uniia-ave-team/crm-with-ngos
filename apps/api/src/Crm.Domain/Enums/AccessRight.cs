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
    ManageRole = 8,

    // --- Ngo ---
    ViewNgo = 9,
    CreateNgo = 10,
    UpdateNgo = 11,

    // --- Login Page Images ---
    ViewLoginPageImages = 12,
    CreateLoginPageImages = 13,
    DeleteLoginPageImages = 14,
}
