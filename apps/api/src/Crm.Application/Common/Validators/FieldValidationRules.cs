using Crm.Domain.Constants.Validation;
using Crm.Domain.Interfaces.Entities;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for incoming dynamic field definitions implementing <see cref="IFieldDefinition"/>.
/// Field storage keys are generated automatically by the server and are not part of client input.
/// </summary>
/// <typeparam name="TField">The field definition type being validated.</typeparam>
public sealed class FieldValidationRules<TField> : AbstractValidator<TField>
    where TField : IFieldDefinition
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValidationRules{TField}"/> class.
    /// </summary>
    public FieldValidationRules()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(FieldSchemaValidationConstants.MaxNameLength)
            .NotContainNullChars();
    }
}
