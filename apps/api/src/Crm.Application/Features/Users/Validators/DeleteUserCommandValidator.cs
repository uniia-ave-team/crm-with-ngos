using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteUserCommand"/>.
/// </summary>
public sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteUserCommandValidator"/> class.
    /// </summary>
    public DeleteUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
