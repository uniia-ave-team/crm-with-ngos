using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RevokeActionTokenCommand"/>.
/// </summary>
public sealed class RevokeActionTokenCommandValidator : AbstractValidator<RevokeActionTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RevokeActionTokenCommandValidator"/> class.
    /// </summary>
    public RevokeActionTokenCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Action token ID is required.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Invalid action token type.");
    }
}
