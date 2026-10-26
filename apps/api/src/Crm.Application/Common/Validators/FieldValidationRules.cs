using System.Text.RegularExpressions;
using Crm.Domain.Consts.Entities;
using Crm.Domain.Interfaces;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for objects implementing <see cref="IFieldSchema"/>.
/// </summary>
public partial class FieldValidationRules<TField> : AbstractValidator<TField>
    where TField : IFieldSchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValidationRules{TField}"/> class.
    /// </summary>
    public FieldValidationRules()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(CustomFieldValidationConstants.MaxNameLength);

        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(CustomFieldValidationConstants.MaxKeyLength)
            .Matches(KeyRegex()).WithMessage("Key must be snake_case.");

        RuleFor(x => x.DataType)
            .IsInEnum();
    }

    [GeneratedRegex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant)]
    public static partial Regex KeyRegex();
}
