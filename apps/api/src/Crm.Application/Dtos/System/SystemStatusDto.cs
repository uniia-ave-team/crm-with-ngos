namespace Crm.Application.Dtos.System;

/// <summary>
/// Represents the current onboarding status, running version, and active maintenance notice of the CRM system.
/// </summary>
public record SystemStatusDto(
    bool HasAdmin,
    bool HasNgo,
    bool IsSetupComplete,
    string Version,
    MaintenanceNoticeDto? Notice);
