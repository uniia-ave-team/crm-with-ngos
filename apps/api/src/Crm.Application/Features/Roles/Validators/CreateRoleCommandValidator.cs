using Crm.Application.Common.Consts;
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
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(AuthRoleValidationConstants.MaxNameLength)
            .WithMessage($"Role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.")
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
