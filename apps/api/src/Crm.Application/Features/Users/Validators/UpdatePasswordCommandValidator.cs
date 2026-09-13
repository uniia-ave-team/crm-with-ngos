using Crm.Application.Common.Options;
using Crm.Application.Dtos.User.Commands;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UpdatePasswordCommand"/>.
/// </summary>
public class UpdatePasswordCommandValidator : AbstractValidator<UpdatePasswordCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePasswordCommandValidator"/> class.
    /// </summary>
    public UpdatePasswordCommandValidator(IOptions<CustomIdentityOptions> identityOptions)
    {
        var options = identityOptions.Value;

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
