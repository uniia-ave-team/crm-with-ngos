using Crm.Application.Dtos.Role.Commands;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RemoveClaimFromRoleCommand"/>.
/// </summary>
public class RemoveClaimFromRoleCommandValidator : AbstractValidator<RemoveClaimFromRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveClaimFromRoleCommandValidator"/> class.
    /// </summary>
    public RemoveClaimFromRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.ClaimValue)
            .NotEmpty().WithMessage("Claim value is required.");
    }
}
