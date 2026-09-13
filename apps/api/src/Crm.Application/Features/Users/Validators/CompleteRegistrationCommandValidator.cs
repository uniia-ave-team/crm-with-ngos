using Crm.Application.Common.Options;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CompleteRegistrationCommand"/>.
/// </summary>
public class CompleteRegistrationCommandValidator : AbstractValidator<CompleteRegistrationCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteRegistrationCommandValidator"/> class.
    /// </summary>
    /// <param name="identityOptions">The configuration options for identity and password rules.</param>
    public CompleteRegistrationCommandValidator(IOptions<CustomIdentityOptions> identityOptions)
    {
        var options = identityOptions.Value;

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Registration token is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(UserValidationConstants.MaxFirstNameLength)
                .WithMessage($"First name must not exceed {UserValidationConstants.MaxFirstNameLength} characters.");

        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(UserValidationConstants.MaxLastNameLength)
                .WithMessage($"Last name must not exceed {UserValidationConstants.MaxLastNameLength} characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(options.RequiredLength)
                .WithMessage($"Password must be at least {options.RequiredLength} characters long.");

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
