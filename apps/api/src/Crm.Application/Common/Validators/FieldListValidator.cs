using Crm.Domain.Consts.Entities.ValidationConstants;
using Crm.Domain.Interfaces;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for collections of fields implementing <see cref="IFieldSchema"/>.
/// </summary>
/// <typeparam name="TField">The type of the field schema, implementing <see cref="IFieldSchema"/>.</typeparam>
public class FieldListValidator<TField> : AbstractValidator<IReadOnlyCollection<TField>>
    where TField : IFieldSchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldListValidator{TField}"/> class and defines validation rules for field uniqueness.
    /// </summary>
    /// <param name="fieldValidator">Optional custom validator for individual field items.</param>
    public FieldListValidator(IValidator<TField>? fieldValidator = null)
    {
        RuleFor(fields => fields)
            .NotNull()
            .WithMessage("Field collection cannot be null.");

        RuleForEach(fields => fields)
            .SetValidator(fieldValidator ?? new FieldValidationRules<TField>());

        RuleFor(fields => fields)
            .Must(HaveValidFieldCount)
            .WithMessage($"Schema cannot contain more than {CustomRecordValidationConstants.MaxFields} fields.")
            .Must(HaveUniqueKeys)
            .WithMessage("Field keys must be unique within the schema.")
            .Must(HaveUniqueOrders)
            .WithMessage("Field display orders must be unique within the schema.");
    }

    /// <summary>
    /// Validates that the number of fields in the collection does not exceed the maximum allowed threshold.
    /// </summary>
    private static bool HaveValidFieldCount(IReadOnlyCollection<TField> fields)
    {
        return fields.Count <= CustomRecordValidationConstants.MaxFields;
    }

    /// <summary>
    /// Validates that all field keys in the collection are unique (case-insensitive).
    /// </summary>
    private static bool HaveUniqueKeys(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<string>(fields.Count, StringComparer.OrdinalIgnoreCase);
        return fields.All(f => f.Key is not null && set.Add(f.Key));
    }

    /// <summary>
    /// Validates that all field display orders in the collection are unique.
    /// </summary>
    private static bool HaveUniqueOrders(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<byte>(fields.Count);
        return fields.All(f => set.Add(f.Order));
    }
}
