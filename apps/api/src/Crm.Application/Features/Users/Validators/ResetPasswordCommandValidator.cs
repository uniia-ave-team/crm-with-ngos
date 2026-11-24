using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="ResetPasswordCommand"/>.
/// </summary>
public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPasswordCommandValidator"/> class.
    /// </summary>
    /// <param name="optionService">The service used to retrieve application configuration options and password requirements.</param>
    public ResetPasswordCommandValidator(IOptionService optionService)
    {
        var options = optionService.GetIdentityOptions();

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Password reset token is required.");

        RuleFor(x => x.NewPassword)
            .ApplyPasswordRules(options);
    }
}
