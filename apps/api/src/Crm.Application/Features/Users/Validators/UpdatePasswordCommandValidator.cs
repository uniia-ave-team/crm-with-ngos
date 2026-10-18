using Crm.Application.Dtos.User.Commands;
using Crm.Application.Interfaces;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdatePasswordCommand"/>.
/// Validates user identification, current password presence, and enforces new password requirements.
/// </summary>
public class UpdatePasswordCommandValidator : AbstractValidator<UpdatePasswordCommand>
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
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(options.RequiredLength)
                .WithMessage($"New password must be at least {options.RequiredLength} characters long.")
            .NotEqual(x => x.CurrentPassword)
                .WithMessage("New password must be different from the current password.");
    }
}
