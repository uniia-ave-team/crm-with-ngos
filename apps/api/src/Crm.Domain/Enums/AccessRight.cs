namespace Crm.Domain.Enums;

/// <summary>
/// Represents all available global NGO system permissions assigned to roles.
/// </summary>
public enum AccessRight : byte
{
    // --- User ---

    /// <summary>
    /// Allows viewing user profiles and user lists.
    /// </summary>
    ViewUser = 0,

    /// <summary>
    /// Allows inviting and creating new user accounts.
    /// </summary>
    CreateUser = 1,

    /// <summary>
    /// Allows editing user profiles and details.
    /// </summary>
    UpdateUser = 2,

    /// <summary>
    /// Allows deactivating and reactivating user accounts.
    /// </summary>
    DisableUser = 3,

    /// <summary>
    /// Allows permanently deleting user accounts.
    /// </summary>
    DeleteUser = 4,

    /// <summary>
    /// Allows assigning and removing roles for users.
    /// </summary>
    AssignRoleToUser = 5,

    /// <summary>
    /// Allows viewing users' emergency contact information.
    /// </summary>
    ViewEmergencyContact = 6,

    /// <summary>
    /// Allows viewing private custom fields on user profiles.
    /// </summary>
    ViewCustomFields = 7,

    /// <summary>
    /// Allows initiating password resets for other users.
    /// </summary>
    CanResetPassword = 8,

    // --- Role ---

    /// <summary>
    /// Allows creating, updating, deleting roles and managing their permissions.
    /// </summary>
    ManageRole = 9,

    // --- Ngo ---

    /// <summary>
    /// Allows creating the primary NGO instance during initial setup.
    /// </summary>
    CreateNgo = 10,

    /// <summary>
    /// Allows updating the NGO name and logo.
    /// </summary>
    UpdateNgo = 11,

    // --- Login Page Images ---

    /// <summary>
    /// Allows viewing login page background images.
    /// </summary>
    ViewLoginPageImages = 12,

    /// <summary>
    /// Allows uploading new login page background images.
    /// </summary>
    CreateLoginPageImages = 13,

    /// <summary>
    /// Allows deleting login page background images.
    /// </summary>
    DeleteLoginPageImages = 14,

    // --- Table Templates ---

    /// <summary>
    /// Allows viewing reusable table templates.
    /// </summary>
    ViewTableTemplate = 15,

    /// <summary>
    /// Allows creating custom table templates.
    /// </summary>
    CreateTableTemplate = 16,

    /// <summary>
    /// Allows updating non-system table templates.
    /// </summary>
    UpdateTableTemplate = 17,

    /// <summary>
    /// Allows deleting non-system table templates.
    /// </summary>
    DeleteTableTemplate = 18,

    // --- Custom Tables & Schema ---

    /// <summary>
    /// Allows viewing NGO custom tables and their schemas.
    /// </summary>
    ViewCustomTable = 19,

    /// <summary>
    /// Allows creating new custom tables.
    /// </summary>
    CreateCustomTable = 20,

    /// <summary>
    /// Allows modifying custom table details and field schemas.
    /// </summary>
    UpdateCustomTable = 21,

    /// <summary>
    /// Allows deleting custom tables and all their records.
    /// </summary>
    DeleteCustomTable = 22,

    // --- Custom Records ---

    /// <summary>
    /// Allows viewing records stored inside custom tables.
    /// </summary>
    ViewCustomRecord = 23,

    /// <summary>
    /// Allows creating new records inside custom tables.
    /// </summary>
    CreateCustomRecord = 24,

    /// <summary>
    /// Allows updating records inside custom tables.
    /// </summary>
    UpdateCustomRecord = 25,

    /// <summary>
    /// Allows deleting records from custom tables.
    /// </summary>
    DeleteCustomRecord = 26,

    // --- Projects ---

    /// <summary>
    /// Allows viewing projects accessible to the user.
    /// </summary>
    ViewProject = 27,

    /// <summary>
    /// Allows creating new projects.
    /// </summary>
    CreateProject = 28,

    /// <summary>
    /// Allows updating projects where the user has edit or manage access.
    /// </summary>
    UpdateProject = 29,

    /// <summary>
    /// Allows deleting projects where the user has manage access.
    /// </summary>
    DeleteProject = 30,

    // --- Tasks ---

    /// <summary>
    /// Allows viewing tasks, comments, and attachments in accessible projects.
    /// </summary>
    ViewTask = 31,

    /// <summary>
    /// Allows creating tasks within editable projects.
    /// </summary>
    CreateTask = 32,

    /// <summary>
    /// Allows updating tasks, statuses, assignees, tags, comments, and attachments.
    /// </summary>
    UpdateTask = 33,

    /// <summary>
    /// Allows deleting tasks within editable projects.
    /// </summary>
    DeleteTask = 34,
}
