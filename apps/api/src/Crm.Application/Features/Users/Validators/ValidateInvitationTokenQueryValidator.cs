using Crm.Application.Dtos.User.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="ValidateInvitationTokenQuery"/>.
/// </summary>
public class ValidateInvitationTokenQueryValidator : AbstractValidator<ValidateInvitationTokenQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateInvitationTokenQueryValidator"/> class.
    /// </summary>
    public ValidateInvitationTokenQueryValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required.");
    }
}
