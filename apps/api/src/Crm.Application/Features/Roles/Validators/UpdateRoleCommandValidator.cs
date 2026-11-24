using Crm.Application.Common.Consts;
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
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("New role name is required.")
            .MaximumLength(AuthRoleValidationConstants.MaxNameLength)
            .WithMessage($"New role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.")
            .Must(v => !v.Contains('\0'))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed);

        RuleFor(x => x.FeminitiveName)
            .Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("Feminitive name cannot be empty or whitespace when provided.")
            .MaximumLength(AuthRoleValidationConstants.MaxFeminitiveNameLength)
            .WithMessage($"Feminitive name must not exceed {AuthRoleValidationConstants.MaxFeminitiveNameLength} characters.")
            .Must(v => v is null || !v.Contains('\0'))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => x.FeminitiveName is not null);

        RuleFor(x => x.PluralName)
            .Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v))
            .WithMessage("Plural name cannot be empty or whitespace when provided.")
            .MaximumLength(AuthRoleValidationConstants.MaxPluralNameLength)
            .WithMessage($"Plural name must not exceed {AuthRoleValidationConstants.MaxPluralNameLength} characters.")
            .Must(v => v is null || !v.Contains('\0'))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => x.PluralName is not null);
    }
}
