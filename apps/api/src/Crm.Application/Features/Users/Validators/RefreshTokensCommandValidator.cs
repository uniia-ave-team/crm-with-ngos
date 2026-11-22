using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RefreshTokensCommand"/>.
/// </summary>
public sealed class RefreshTokensCommandValidator : AbstractValidator<RefreshTokensCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshTokensCommandValidator"/> class.
    /// </summary>
    public RefreshTokensCommandValidator()
    {
        RuleFor(x => x.AccessToken)
            .NotEmpty().WithMessage("Access token is required.");

        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh token is required.");
    }
}
