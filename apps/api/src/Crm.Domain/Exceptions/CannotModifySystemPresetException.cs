namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when an attempt is made to modify or delete a system preset entity.
/// </summary>
public class CannotModifySystemPresetException(Guid templateId)
    : DomainValidationException(FormatMessage(templateId))
{
    private static string FormatMessage(Guid templateId) =>
        $"Table template with ID '{templateId}' cannot be modified or deleted because it is a system preset.";
}
