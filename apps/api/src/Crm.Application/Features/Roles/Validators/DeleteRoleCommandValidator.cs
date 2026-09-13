using Crm.Application.Dtos.Role.Commands;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteRoleCommand"/>.
/// </summary>
public class DeleteRoleCommandValidator : AbstractValidator<DeleteRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteRoleCommandValidator"/> class.
    /// </summary>
    public DeleteRoleCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required.");
    }
}
