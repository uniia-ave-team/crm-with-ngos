using Crm.Domain.Exceptions;
using Crm.Domain.Interfaces.Entities;

namespace Crm.Domain.ValueObjects;

/// <summary>
/// Represents an immutable field blueprint stored inside a <see cref="Entities.TableTemplate"/> JSONB column.
/// </summary>
public sealed record TemplateFieldDefinition : IFieldDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateFieldDefinition"/> class.
    /// </summary>
    /// <param name="name">The human-readable display name of the field.</param>
    /// <param name="isRequired">Whether the field requires a value when instantiated into a table.</param>
    /// <param name="order">The zero-based display order of the field.</param>
    public TemplateFieldDefinition(string name, bool isRequired, byte order)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Template field name cannot be empty.");
        }

        Name = name.Trim();
        IsRequired = isRequired;
        Order = order;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TemplateFieldDefinition"/> class for EF Core JSON materialization.
    /// </summary>
    private TemplateFieldDefinition()
    {
    }

    /// <inheritdoc />
    public string Name { get; init; } = string.Empty;

    /// <inheritdoc />
    public bool IsRequired { get; init; }

    /// <inheritdoc />
    public byte Order { get; init; }
}
