namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a scheduled maintenance window has an end timestamp that is not strictly after its start timestamp.
/// </summary>
public sealed class InvalidMaintenanceWindowException()
    : DomainValidationException("Maintenance window end time must be strictly after start time.");
