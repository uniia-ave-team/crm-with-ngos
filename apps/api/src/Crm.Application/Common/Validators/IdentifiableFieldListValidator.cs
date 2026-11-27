using Crm.Domain.Interfaces.Entities;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for collections of identifiable field update DTOs implementing <see cref="IIdentifiableField"/>.
/// </summary>
/// <typeparam name="TField">The type of the field update DTO, implementing <see cref="IIdentifiableField"/>.</typeparam>
public sealed class IdentifiableFieldListValidator<TField> : FieldListValidator<TField>
    where TField : IIdentifiableField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentifiableFieldListValidator{TField}"/> class.
    /// </summary>
    public IdentifiableFieldListValidator()
    {
        RuleFor(fields => fields)
            .Must(NotContainEmptyGuidIds)
            .WithMessage("Field ID cannot be an empty GUID when provided.")
            .Must(HaveUniqueIds)
            .WithMessage("Field IDs must be unique within the schema.")
            .When(fields => fields is not null);
    }

    private static bool NotContainEmptyGuidIds(IReadOnlyCollection<TField> fields)
        => fields.All(f => f is not null && (!f.Id.HasValue || f.Id.Value != Guid.Empty));

    private static bool HaveUniqueIds(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<Guid>();
        return fields
            .Where(f => f is not null && f.Id.HasValue && f.Id.Value != Guid.Empty)
            .All(f => set.Add(f.Id!.Value));
    }
}
