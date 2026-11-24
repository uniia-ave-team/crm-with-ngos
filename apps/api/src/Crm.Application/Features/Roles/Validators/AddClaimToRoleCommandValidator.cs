using Crm.Application.Features.Roles.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="AddClaimToRoleCommand"/>.
/// </summary>
public sealed class AddClaimToRoleCommandValidator : AbstractValidator<AddClaimToRoleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddClaimToRoleCommandValidator"/> class.
    /// </summary>
    public AddClaimToRoleCommandValidator()
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
