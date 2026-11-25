using System.Text.RegularExpressions;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Entities;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for objects implementing <see cref="IFieldSchema"/>.
/// </summary>
/// <typeparam name="TField">The field schema type being validated.</typeparam>
public sealed partial class FieldValidationRules<TField> : AbstractValidator<TField>
    where TField : IFieldSchema
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FieldValidationRules{TField}"/> class.
    /// </summary>
    /// <param name="isReservedKey">
    /// Optional predicate determining whether a field key collides with a reserved entity property.
    /// Defaults to <see cref="CustomFieldExtensions.IsReservedKey"/>.
    /// </param>
    public FieldValidationRules(Func<string?, bool>? isReservedKey = null)
    {
        var reservedCheck = isReservedKey ?? CustomFieldExtensions.IsReservedKey;

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(FieldSchemaValidationConstants.MaxNameLength)
            .NotContainNullChars();

        RuleFor(x => x.Key)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(FieldSchemaValidationConstants.MaxKeyLength)
            .Matches(KeyRegex()).WithMessage("Key must be snake_case.")
            .Must(k => !reservedCheck(k)).WithMessage("Key cannot be a reserved entity property name.");

        RuleFor(x => x.DataType)
            .IsInEnum();
    }

    /// <summary>
    /// Compiled regular expression matching valid snake_case field keys.
    /// </summary>
    /// <returns>The compiled <see cref="Regex"/> instance.</returns>
    [GeneratedRegex(@"^[a-z][a-z0-9_]*\z", RegexOptions.CultureInvariant)]
    public static partial Regex KeyRegex();
}
