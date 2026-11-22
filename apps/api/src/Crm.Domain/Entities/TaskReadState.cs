namespace Crm.Domain.Entities;

/// <summary>
/// Represents an unread marker linking an unread <see cref="TaskComment"/> to a <see cref="Entities.User"/>.
/// Rows exist only while the comment is unread and are deleted once the user reads comments on the task.
/// </summary>
public class TaskReadState
{
    /// <summary>
    /// Gets or sets the foreign key identifier of the unread <see cref="TaskComment"/>.
    /// </summary>
    public Guid CommentId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the unread <see cref="TaskComment"/>.
    /// </summary>
    public TaskComment Comment { get; set; } = null!;

    /// <summary>
    /// Gets or sets the foreign key identifier of the <see cref="Entities.User"/>.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the navigation property pointing to the <see cref="Entities.User"/>.
    /// </summary>
    public User User { get; set; } = null!;
}
