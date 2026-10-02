using Crm.Application.Dtos.User.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="AssignUserRoleCommand"/>.
/// </summary>
public class AssignUserRoleCommandValidator : AbstractValidator<AssignUserRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignUserRoleCommandValidator"/> class.
    /// </summary>
    public AssignUserRoleCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");
    }
}
