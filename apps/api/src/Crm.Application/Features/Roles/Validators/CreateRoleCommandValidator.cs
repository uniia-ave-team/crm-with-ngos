using Crm.Application.Common.Validators;
using Crm.Application.Features.Roles.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateRoleCommand"/>.
/// </summary>
public sealed class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoleCommandValidator"/> class.
    /// </summary>
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .ApplyRequiredTextRules(
                AuthRoleValidationConstants.MaxNameLength,
                "Role name is required.",
                $"Role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.FeminitiveName)
            .ApplyOptionalNonBlankTextRules(
                AuthRoleValidationConstants.MaxFeminitiveNameLength,
                "Feminitive name cannot be empty or whitespace when provided.",
                $"Feminitive name must not exceed {AuthRoleValidationConstants.MaxFeminitiveNameLength} characters.")
            .When(x => x.FeminitiveName is not null);

        RuleFor(x => x.PluralName)
            .ApplyOptionalNonBlankTextRules(
                AuthRoleValidationConstants.MaxPluralNameLength,
                "Plural name cannot be empty or whitespace when provided.",
                $"Plural name must not exceed {AuthRoleValidationConstants.MaxPluralNameLength} characters.")
            .When(x => x.PluralName is not null);
    }
}
