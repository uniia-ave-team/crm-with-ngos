namespace Crm.Domain.Entities;

/// <summary>
/// Represents a user comment posted on a <see cref="ProjectTask"/>.
/// </summary>
public class TaskComment : BaseEntity
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="ProjectTask"/>.
    /// </summary>
    public Guid TaskId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the parent <see cref="ProjectTask"/>.
    /// </summary>
    public ProjectTask Task { get; set; }

    /// <summary>
    /// Gets or sets the optional foreign key identifier of the author <see cref="User"/>.
    /// Set to <c>null</c> if the author is deleted.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the author <see cref="User"/>.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the text content of the comment.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the comment was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the comment was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the per-user unread markers for this comment; rows are removed once read.
    /// </summary>
    public ICollection<TaskReadState> ReadStates { get; set; } = [];
}
