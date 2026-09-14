using Crm.Application.Common.Options;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateUserCommand"/>.
/// </summary>
public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateUserCommandValidator"/> class.
    /// </summary>
    public CreateUserCommandValidator(IOptions<CustomIdentityOptions> identityOptions)
    {
        var options = identityOptions.Value;

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(UserValidationConstants.MaxFirstNameLength);

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(UserValidationConstants.MaxLastNameLength);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(AuthUserValidationConstants.MaxEmailLength);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(options.RequiredLength)
            .WithMessage($"Password must be at least {options.RequiredLength} characters long.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage("Confirm password is required.")
            .Equal(x => x.Password).WithMessage("Passwords do not match.");

        if (options.RequireDigit)
        {
            RuleFor(x => x.Password)
                .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        }

        if (options.RequireUppercase)
        {
            RuleFor(x => x.Password)
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.");
        }

        if (options.RequireLowercase)
        {
            RuleFor(x => x.Password)
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.");
        }

        if (options.RequireNonAlphanumeric)
        {
            RuleFor(x => x.Password)
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one non-alphanumeric character.");
        }
    }
}
