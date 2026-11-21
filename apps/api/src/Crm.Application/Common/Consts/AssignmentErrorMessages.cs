namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides shared error message constants for project and task user assignment validation.
/// </summary>
public static class AssignmentErrorMessages
{
    /// <summary>
    /// Error message when a target user account is deactivated.
    /// </summary>
    public const string UserAccountDeactivated = "The user account is deactivated.";

    /// <summary>
    /// Formats an error message when a user lacks membership access to a private project.
    /// </summary>
    /// <param name="projectId">The unique identifier of the private project.</param>
    /// <returns>The formatted error message.</returns>
    public static string NoPrivateProjectAccess(Guid projectId)
        => $"The user does not have access to private project '{projectId}'.";
}
