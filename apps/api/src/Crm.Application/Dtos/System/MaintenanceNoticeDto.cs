namespace Crm.Application.Dtos.System;

/// <summary>
/// Represents an active or upcoming scheduled maintenance notice exposed via system status.
/// </summary>
public record MaintenanceNoticeDto(
    string Message,
    DateTime StartsAt,
    DateTime EndsAt);
