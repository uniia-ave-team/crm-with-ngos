namespace Crm.Domain.Enums;

/// <summary>
/// Represents the predefined, non-customizable execution status of a project task.
/// </summary>
public enum ProjectTaskStatus : byte
{
    /// <summary>
    /// The task is queued to be done.
    /// </summary>
    ToDo = 0,

    /// <summary>
    /// The task is currently in progress.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Progress on the task is blocked.
    /// </summary>
    Blocked = 2,

    /// <summary>
    /// The execution work on the task is finished and awaiting review or final sign-off.
    /// Unlike <see cref="Completed"/>, transitioning to <see cref="Done"/> does not automatically set <c>EndDate</c>.
    /// </summary>
    Done = 3,

    /// <summary>
    /// The task is finally completed and accepted.
    /// Transitioning to <see cref="Completed"/> automatically populates <c>EndDate</c> when none is set.
    /// </summary>
    Completed = 4,
}
