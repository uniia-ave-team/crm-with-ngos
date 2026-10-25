using Crm.Domain.Interfaces;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for collections of fields implementing <see cref="IIdentifiableField"/>.
/// </summary>
/// <typeparam name="TField">The type of the field schema, implementing <see cref="IIdentifiableField"/>.</typeparam>
public class IdentifiableFieldListValidator<TField> : FieldListValidator<TField>
    where TField : IIdentifiableField
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IdentifiableFieldListValidator{TField}"/> class.
    /// </summary>
    public IdentifiableFieldListValidator()
    {
        RuleFor(fields => fields)
            .Must(HaveUniqueIds)
            .WithMessage("Field IDs must be unique within the schema.");
    }

    /// <summary>
    /// Validates that all non-null and non-empty field IDs in the collection are unique.
    /// </summary>
    /// <param name="fields">The collection of fields to validate.</param>
    /// <returns><see langword="true"/> if all specified field IDs are unique; otherwise, <see langword="false"/>.</returns>
    private static bool HaveUniqueIds(IReadOnlyCollection<TField> fields)
    {
        var set = new HashSet<Guid>();
        return fields
            .Where(f => f.Id.HasValue && f.Id.Value != Guid.Empty)
            .All(f => set.Add(f.Id!.Value));
    }
}
