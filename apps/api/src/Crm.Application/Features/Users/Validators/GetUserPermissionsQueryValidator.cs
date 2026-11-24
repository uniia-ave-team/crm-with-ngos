using Crm.Application.Features.Users.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUserPermissionsQuery"/>.
/// </summary>
public sealed class GetUserPermissionsQueryValidator : AbstractValidator<GetUserPermissionsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserPermissionsQueryValidator"/> class.
    /// </summary>
    public GetUserPermissionsQueryValidator()
    {
        RuleFor(x => x.RoleIds)
            .NotNull().WithMessage("Role IDs collection cannot be null.");

        RuleForEach(x => x.RoleIds)
            .NotEmpty().WithMessage("Role ID cannot be empty.")
            .When(x => x.RoleIds is not null);
    }
}
