using Crm.Application.Dtos.Role.Queries;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetRoleByIdQuery"/>.
/// </summary>
public class GetRoleByIdQueryValidator : AbstractValidator<GetRoleByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetRoleByIdQueryValidator"/> class.
    /// </summary>
    public GetRoleByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Role ID is required.");
    }
}
