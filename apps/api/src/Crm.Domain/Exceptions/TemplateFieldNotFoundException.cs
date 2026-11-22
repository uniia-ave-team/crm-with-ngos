namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested field ID is not found within the specified table template.
/// </summary>
/// <param name="fieldId">The unique identifier of the missing template field.</param>
/// <param name="templateId">The unique identifier of the parent table template.</param>
public sealed class TemplateFieldNotFoundException(Guid fieldId, Guid templateId)
    : NotFoundException($"Template field with ID '{fieldId}' was not found in table template with ID '{templateId}'.");
