namespace Crm.Application.Dtos.System;

/// <summary>
/// Represents the current onboarding and setup status of the CRM system.
/// </summary>
public record SystemSetupStatusDto(
    bool HasAdmin,
    bool HasNgo,
    bool IsSetupComplete);
