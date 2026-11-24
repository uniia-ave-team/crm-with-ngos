using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateUserCommand"/>.
/// Validates user personal details, email format, and enforces configured password complexity policies.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateUserCommandValidator"/> class.
    /// </summary>
    /// <param name="optionService">The service used to retrieve application configuration options and password requirements.</param>
    public CreateUserCommandValidator(IOptionService optionService)
    {
        var options = optionService.GetIdentityOptions();

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(UserValidationConstants.MaxFirstNameLength);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(UserValidationConstants.MaxLastNameLength);

        RuleFor(x => x.Patronymic)
            .MaximumLength(UserValidationConstants.MaxPatronymicLength)
            .When(x => !string.IsNullOrEmpty(x.Patronymic));

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(AuthUserValidationConstants.MaxEmailLength);

        RuleFor(x => x.Password)
            .ApplyPasswordRules(options);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
