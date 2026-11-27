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

    /// <summary>
    /// Allows viewing and revoking active user action tokens (invitations and password reset tokens).
    /// </summary>
    ManageUserActionTokens = 9,

    // --- Role ---

    /// <summary>
    /// Allows creating, updating, deleting roles and managing their permissions.
    /// </summary>
    ManageRole = 10,

    // --- Ngo ---

    /// <summary>
    /// Allows creating the primary NGO instance during initial setup.
    /// </summary>
    CreateNgo = 11,

    /// <summary>
    /// Allows updating the NGO name and logo.
    /// </summary>
    UpdateNgo = 12,

    // --- Login Page Images ---

    /// <summary>
    /// Allows viewing login page background images.
    /// </summary>
    ViewLoginPageImages = 13,

    /// <summary>
    /// Allows uploading new login page background images.
    /// </summary>
    CreateLoginPageImages = 14,

    /// <summary>
    /// Allows deleting login page background images.
    /// </summary>
    DeleteLoginPageImages = 15,

    // --- Table Templates ---

    /// <summary>
    /// Allows viewing reusable table templates.
    /// </summary>
    ViewTableTemplate = 16,

    /// <summary>
    /// Allows creating custom table templates.
    /// </summary>
    CreateTableTemplate = 17,

    /// <summary>
    /// Allows updating non-system table templates.
    /// </summary>
    UpdateTableTemplate = 18,

    /// <summary>
    /// Allows deleting non-system table templates.
    /// </summary>
    DeleteTableTemplate = 19,

    // --- Custom Tables & Schema ---

    /// <summary>
    /// Allows viewing NGO custom tables and their schemas.
    /// </summary>
    ViewCustomTable = 20,

    /// <summary>
    /// Allows creating new custom tables.
    /// </summary>
    CreateCustomTable = 21,

    /// <summary>
    /// Allows modifying custom table details and field schemas.
    /// </summary>
    UpdateCustomTable = 22,

    /// <summary>
    /// Allows deleting custom tables and all their records.
    /// </summary>
    DeleteCustomTable = 23,

    // --- Custom Records ---

    /// <summary>
    /// Allows viewing records stored inside custom tables.
    /// </summary>
    ViewCustomRecord = 24,

    /// <summary>
    /// Allows creating new records inside custom tables.
    /// </summary>
    CreateCustomRecord = 25,

    /// <summary>
    /// Allows updating records inside custom tables.
    /// </summary>
    UpdateCustomRecord = 26,

    /// <summary>
    /// Allows deleting records from custom tables.
    /// </summary>
    DeleteCustomRecord = 27,

    // --- Projects ---

    /// <summary>
    /// Allows viewing projects accessible to the user.
    /// </summary>
    ViewProject = 28,

    /// <summary>
    /// Allows creating new projects.
    /// </summary>
    CreateProject = 29,

    /// <summary>
    /// Allows updating projects where the user has edit or manage access.
    /// </summary>
    UpdateProject = 30,

    /// <summary>
    /// Allows deleting projects where the user has manage access.
    /// </summary>
    DeleteProject = 31,

    // --- Tasks ---

    /// <summary>
    /// Allows viewing tasks, comments, and attachments in accessible projects.
    /// </summary>
    ViewTask = 32,

    /// <summary>
    /// Allows creating tasks within editable projects.
    /// </summary>
    CreateTask = 33,

    /// <summary>
    /// Allows updating tasks, statuses, assignees, tags, comments, and attachments.
    /// </summary>
    UpdateTask = 34,

    /// <summary>
    /// Allows deleting tasks within editable projects.
    /// </summary>
    DeleteTask = 35,
}
