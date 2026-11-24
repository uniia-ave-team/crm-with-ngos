namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides query filter tokens for project task listing endpoints.
/// </summary>
public static class TaskFilterConstants
{
    /// <summary>
    /// The query filter token used to restrict task listings to tasks assigned to the current user.
    /// </summary>
    public const string MineFilterValue = "mine";
}
