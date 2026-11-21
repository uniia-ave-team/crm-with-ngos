namespace Crm.Domain.Consts.Entities.ValidationConstants;

/// <summary>
/// Provides validation and configuration constants for project, member, topic, and tag entities.
/// </summary>
public static class ProjectValidationConstants
{
    /// <summary>
    /// The maximum allowed length for a project title.
    /// </summary>
    public const int MaxTitleLength = 255;

    /// <summary>
    /// The maximum allowed length for a project description.
    /// </summary>
    public const int MaxDescriptionLength = 2000;

    /// <summary>
    /// The maximum allowed length for a project member's custom functional role title.
    /// </summary>
    public const int MaxRoleTitleLength = 150;

    /// <summary>
    /// The maximum allowed length for a board topic title.
    /// </summary>
    public const int MaxTopicTitleLength = 255;

    /// <summary>
    /// The maximum allowed length for a project-scoped tag title.
    /// </summary>
    public const int MaxTagTitleLength = 100;

    /// <summary>
    /// The maximum allowed display order index for a board topic (backed by <see cref="byte"/>).
    /// </summary>
    public const byte MaxTopicOrder = byte.MaxValue;
}
