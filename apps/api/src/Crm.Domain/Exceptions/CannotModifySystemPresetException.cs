namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to modify or delete a system preset entity.
/// </summary>
/// <param name="templateId">The unique identifier of the system preset table template.</param>
public sealed class CannotModifySystemPresetException(Guid templateId)
    : DomainValidationException($"Table template with ID '{templateId}' cannot be modified or deleted because it is a system preset.");
