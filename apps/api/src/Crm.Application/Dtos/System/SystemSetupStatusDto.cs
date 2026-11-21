namespace Crm.Application.Dtos.System;

/// <summary>
/// Represents the current onboarding and setup status of the CRM system.
/// </summary>
/// <param name="HasAdmin">Whether at least one administrator exists.</param>
/// <param name="HasNgo">Whether the organisation profile exists.</param>
/// <param name="IsSetupComplete">Whether the initial setup is finished.</param>
/// <param name="Demo">Details of the public demo mode, or <c>null</c> when it is off.</param>
public record SystemSetupStatusDto(
    bool HasAdmin,
    bool HasNgo,
    bool IsSetupComplete,
    DemoModeInfoDto? Demo = null);
