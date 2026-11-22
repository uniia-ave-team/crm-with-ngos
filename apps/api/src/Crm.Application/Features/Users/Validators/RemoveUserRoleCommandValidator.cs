using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RemoveUserRoleCommand"/>.
/// </summary>
public sealed class RemoveUserRoleCommandValidator : AbstractValidator<RemoveUserRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveUserRoleCommandValidator"/> class.
    /// </summary>
    public RemoveUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");
    }
}
