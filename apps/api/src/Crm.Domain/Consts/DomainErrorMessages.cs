namespace Crm.Domain.Consts;

/// <summary>
/// Provides shared domain error message constants.
/// </summary>
public static class DomainErrorMessages
{
    /// <summary>
    /// Error message when a task's end date precedes its start date.
    /// </summary>
    public const string EndDateBeforeStartDate = "End date cannot be earlier than start date.";

    /// <summary>
    /// Error message when an unsupported project claim enum value is evaluated.
    /// </summary>
    public const string UnsupportedProjectClaim = "Unsupported project claim.";
}
