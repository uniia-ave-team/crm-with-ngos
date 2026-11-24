using Crm.Application.Features.Users.Commands;
using Crm.Application.Interfaces;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdatePasswordCommand"/>.
/// Validates user identification, current password presence, and enforces new password requirements.
/// </summary>
public sealed class UpdatePasswordCommandValidator : AbstractValidator<UpdatePasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePasswordCommandValidator"/> class.
    /// </summary>
    /// <param name="optionService">The service used to retrieve application configuration options and password requirements.</param>
    public UpdatePasswordCommandValidator(IOptionService optionService)
    {
        var options = optionService.GetIdentityOptions();

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .ApplyPasswordRules(options)
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must be different from the current password.");
    }
}
