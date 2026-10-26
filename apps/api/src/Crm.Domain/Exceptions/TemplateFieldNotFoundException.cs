namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested field ID is not found within the specified table template.
/// </summary>
public class TemplateFieldNotFoundException(Guid fieldId, Guid templateId)
    : NotFoundException($"Template field with ID '{fieldId}' was not found in table template with ID '{templateId}'.");
