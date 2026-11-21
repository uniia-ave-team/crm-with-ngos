namespace Crm.Domain.Enums;

/// <summary>
/// Represents project-level permission claims assigned to a project member.
/// Separate from the global NGO <see cref="AccessRight"/> authorization framework.
/// </summary>
public enum ProjectClaim : byte
{
    /// <summary>
    /// Grants read access to a private project and its tasks, topics, tags, attachments, and comments
    /// (including posting and managing one's own comments).
    /// </summary>
    View = 0,

    /// <summary>
    /// Grants write access to create, update, and delete tasks, topics, tags, comments, and attachments within a project.
    /// </summary>
    Edit = 1,

    /// <summary>
    /// Grants administrative access to manage project settings, visibility, and project members.
    /// </summary>
    Manage = 2,
}
