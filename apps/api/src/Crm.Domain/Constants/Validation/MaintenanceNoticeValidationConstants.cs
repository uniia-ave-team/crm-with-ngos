using Crm.Domain.Entities;

namespace Crm.Domain.Constants.Validation;

/// <summary>
/// Validation constraints for <see cref="MaintenanceNotice"/>.
/// </summary>
public static class MaintenanceNoticeValidationConstants
{
    /// <summary>
    /// Maximum allowed length of a maintenance notice message.
    /// </summary>
    public const int MaxMessageLength = 1000;
}
