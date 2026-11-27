using Crm.Application.Common.Validators;
using Crm.Application.Features.Roles.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateRoleCommand"/>.
/// </summary>
public sealed class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRoleCommandValidator"/> class.
    /// </summary>
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.NewName)
            .ApplyRequiredTextRules(
                AuthRoleValidationConstants.MaxNameLength,
                "New role name is required.",
                $"New role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.");

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
