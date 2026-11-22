using Crm.Application.Features.Users.Commands;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="InviteUserCommand"/>.
/// </summary>
public sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InviteUserCommandValidator"/> class.
    /// </summary>
    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email address is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(AuthUserValidationConstants.MaxEmailLength)
            .WithMessage($"Email address must not exceed {AuthUserValidationConstants.MaxEmailLength} characters.");

        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage("At least one Role ID is required.");

        RuleForEach(x => x.RoleIds)
            .NotEmpty().WithMessage("Role ID cannot be empty.")
            .When(x => x.RoleIds is not null);
    }
}
