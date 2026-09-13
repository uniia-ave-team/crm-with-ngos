using Crm.Application.Dtos.Role.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateRoleCommand"/>.
/// </summary>
public class CreateRoleCommandValidator : AbstractValidator<CreateRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateRoleCommandValidator"/> class.
    /// </summary>
    public CreateRoleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Role name is required.")
            .MaximumLength(AuthRoleValidationConstants.MaxNameLength)
            .WithMessage($"Role name must not exceed {AuthRoleValidationConstants.MaxNameLength} characters.");
    }
}
