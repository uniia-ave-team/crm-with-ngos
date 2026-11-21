namespace Crm.Domain.Consts.Entities.GeneralConstants;

/// <summary>
/// Provides general constants related to the <see cref="Domain.Entities.ProjectTask"/> entity and query operations.
/// </summary>
public static class TaskConstants
{
    /// <summary>
    /// The query filter token used to restrict task listings to tasks assigned to the current user.
    /// </summary>
    public const string MineFilterValue = "mine";
}
