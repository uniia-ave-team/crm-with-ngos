using Crm.Application.Dtos.Role.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdateRoleCommand"/>.
/// </summary>
public class UpdateRoleCommandValidator : AbstractValidator<UpdateRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRoleCommandValidator"/> class.
    /// </summary>
    public UpdateRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("New role name is required.")
            .MaximumLength(AuthRoleValidationConstants.MaxNameLength)
            .WithMessage($"New role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.");

        RuleFor(x => x.FeminitiveName)
            .MaximumLength(AuthRoleValidationConstants.MaxFeminitiveNameLength)
            .WithMessage($"Feminitive name must not exceed {AuthRoleValidationConstants.MaxFeminitiveNameLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.FeminitiveName));

        RuleFor(x => x.PluralName)
            .MaximumLength(AuthRoleValidationConstants.MaxPluralNameLength)
            .WithMessage($"Plural name must not exceed {AuthRoleValidationConstants.MaxPluralNameLength} characters.")
            .When(x => !string.IsNullOrEmpty(x.PluralName));
    }
}
