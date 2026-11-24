using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GenerateRestorePasswordTokenCommand"/>.
/// </summary>
public sealed class GenerateRestorePasswordTokenCommandValidator : AbstractValidator<GenerateRestorePasswordTokenCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateRestorePasswordTokenCommandValidator"/> class.
    /// </summary>
    public GenerateRestorePasswordTokenCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
