using Crm.Application.Features.Users.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="AuthenticateUserCommand"/>.
/// </summary>
public sealed class AuthenticateUserCommandValidator : AbstractValidator<AuthenticateUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticateUserCommandValidator"/> class.
    /// </summary>
    public AuthenticateUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(AuthUserValidationConstants.MaxEmailLength);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
