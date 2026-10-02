using Crm.Application.Dtos.User.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for <see cref="ActivateUserCommand"/>.
/// </summary>
public class ActivateUserCommandValidator : AbstractValidator<ActivateUserCommand>
{
    public ActivateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
