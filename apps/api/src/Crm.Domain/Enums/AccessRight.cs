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
    CanResetPassword = 8,

    // --- Role ---
    ManageRole = 9,

    // --- Ngo ---
    ViewNgo = 10,
    CreateNgo = 11,
    UpdateNgo = 12,

    // --- Login Page Images ---
    ViewLoginPageImages = 13,
    CreateLoginPageImages = 14,
    DeleteLoginPageImages = 15,

    // --- Table Templates ---
    ViewTableTemplate = 16,
    CreateTableTemplate = 17,
    UpdateTableTemplate = 18,
    DeleteTableTemplate = 19,

    // --- Custom Tables & Schema ---
    ViewCustomTable = 20,
    CreateCustomTable = 21,
    UpdateCustomTable = 22,
    DeleteCustomTable = 23,

    // --- Custom Records ---
    ViewCustomRecord = 24,
    CreateCustomRecord = 25,
    UpdateCustomRecord = 26,
    DeleteCustomRecord = 27,
}
