using Crm.Application.Features.Roles.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RemoveClaimFromRoleCommand"/>.
/// </summary>
public sealed class RemoveClaimFromRoleCommandValidator : AbstractValidator<RemoveClaimFromRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveClaimFromRoleCommandValidator"/> class.
    /// </summary>
    public RemoveClaimFromRoleCommandValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty().WithMessage("Role ID is required.");

        RuleFor(x => x.ClaimValue)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Claim value is required.")
            .Must(claim => PermissionExtensions.GetAllStringValues().Contains(claim))
            .WithMessage("The specified permission claim does not exist in the system.");
    }
}
