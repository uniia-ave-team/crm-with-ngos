using Crm.Domain.Constants.Validation;
using Crm.Domain.Interfaces.Entities;
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
    /// Initializes a new instance of the <see cref="FieldListValidator{TField}"/> class and defines validation rules for field uniqueness and sequential ordering.
    /// </summary>
    public FieldListValidator()
    {
        RuleFor(fields => fields)
            .NotNull()
            .WithMessage("Field collection cannot be null.");

        RuleForEach(fields => fields)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("Field item cannot be null.")
            .SetValidator(new FieldValidationRules<TField>())
            .When(fields => fields is not null);

        RuleFor(fields => fields)
            .Must(HaveValidFieldCount)
            .WithMessage($"Schema cannot contain more than {CustomRecordValidationConstants.MaxFields} fields.")
            .Must(HaveUniqueKeys)
            .WithMessage("Field keys must be unique within the schema.")
            .Must(HaveUniqueOrders)
            .WithMessage("Field display orders must be unique within the schema.")
            .Must(HaveSequentialOrders)
            .WithMessage("Field display orders must form a contiguous 0-based sequence.")
            .When(fields => fields is not null);
    }

    private static bool HaveValidFieldCount(IReadOnlyCollection<TField> fields)
        => fields.Count <= CustomRecordValidationConstants.MaxFields;

    private static bool HaveUniqueKeys(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<string>(fields.Count, StringComparer.OrdinalIgnoreCase);
        return fields.All(f => f is not null && f.Key is not null && set.Add(f.Key));
    }

    private static bool HaveUniqueOrders(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<byte>(fields.Count);
        return fields.All(f => f is not null && set.Add(f.Order));
    }

    private static bool HaveSequentialOrders(IReadOnlyCollection<TField> fields)
        => fields.All(f => f is not null && f.Order < fields.Count);
}
