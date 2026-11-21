namespace Crm.Domain.Enums;

/// <summary>
/// Represents the predefined priority level of a project task.
/// </summary>
public enum TaskPriority : byte
{
    /// <summary>
    /// Low priority.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium priority.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High priority.
    /// </summary>
    High = 2,

    /// <summary>
    /// Critical priority.
    /// </summary>
    Critical = 3,
}
