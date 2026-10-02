using Crm.Application.Dtos.Role.Commands;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="AddClaimToRoleCommand"/>.
/// </summary>
public class AddClaimToRoleCommandValidator : AbstractValidator<AddClaimToRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddClaimToRoleCommandValidator"/> class.
    /// </summary>
    public AddClaimToRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.ClaimValue)
            .NotEmpty().WithMessage("Claim value is required.");
    }
}
