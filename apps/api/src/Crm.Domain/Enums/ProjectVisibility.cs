namespace Crm.Domain.Enums;

/// <summary>
/// Specifies the access visibility level for viewing and editing a project.
/// </summary>
public enum ProjectVisibility : byte
{
    /// <summary>
    /// Private project with private editing: restricted to project members with appropriate claims.
    /// </summary>
    Private = 0,

    /// <summary>
    /// Public project with private editing: visible to all NGO users, while editing requires project member claims.
    /// </summary>
    PublicViewPrivateEdit = 1,

    /// <summary>
    /// Public project with public editing: accessible for both viewing and editing to all authenticated NGO users.
    /// </summary>
    Public = 2,
}
