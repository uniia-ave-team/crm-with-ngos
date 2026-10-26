using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Role.Queries;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetAllRolesQuery"/>.
/// </summary>
public class GetAllRolesQueryValidator : AbstractValidator<GetAllRolesQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllRolesQueryValidator"/> class.
    /// </summary>
    public GetAllRolesQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());
    }
}
