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
    CreateNgo = 10,
    UpdateNgo = 11,

    // --- Login Page Images ---
    ViewLoginPageImages = 12,
    CreateLoginPageImages = 13,
    DeleteLoginPageImages = 14,

    // --- Table Templates ---
    ViewTableTemplate = 15,
    CreateTableTemplate = 16,
    UpdateTableTemplate = 17,
    DeleteTableTemplate = 18,

    // --- Custom Tables & Schema ---
    ViewCustomTable = 19,
    CreateCustomTable = 20,
    UpdateCustomTable = 21,
    DeleteCustomTable = 22,

    // --- Custom Records ---
    ViewCustomRecord = 23,
    CreateCustomRecord = 24,
    UpdateCustomRecord = 25,
    DeleteCustomRecord = 26,
}
