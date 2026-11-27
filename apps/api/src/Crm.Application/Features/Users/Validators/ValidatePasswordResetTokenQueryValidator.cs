using Crm.Application.Features.Users.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="ValidatePasswordResetTokenQuery"/>.
/// </summary>
public sealed class ValidatePasswordResetTokenQueryValidator : AbstractValidator<ValidatePasswordResetTokenQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ValidatePasswordResetTokenQueryValidator"/> class.
    /// </summary>
    public ValidatePasswordResetTokenQueryValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Password reset token is required.");
    }
}
