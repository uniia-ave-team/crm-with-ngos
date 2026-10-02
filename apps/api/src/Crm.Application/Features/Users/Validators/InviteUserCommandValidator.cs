using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="InviteUserCommand"/>.
/// </summary>
public class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
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
    }
}
