namespace Crm.Application.Dtos.System;

/// <summary>
/// Represents the current onboarding status, running version, and active maintenance notice of the CRM system.
/// </summary>
/// <param name="HasAdmin">Whether at least one administrator exists.</param>
/// <param name="HasNgo">Whether the organisation profile exists.</param>
/// <param name="IsSetupComplete">Whether the initial setup is finished.</param>
/// <param name="Version">The running application version.</param>
/// <param name="Notice">The active scheduled maintenance notice, or <c>null</c>.</param>
/// <param name="Demo">Details of the public demo mode, or <c>null</c> when it is off.</param>
public record SystemStatusDto(
    bool HasAdmin,
    bool HasNgo,
    bool IsSetupComplete,
    string Version,
    MaintenanceNoticeDto? Notice,
    DemoModeInfoDto? Demo = null);
