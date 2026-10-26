using Crm.Application.Common.Validators;
using Crm.Application.Dtos.User.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUsersQuery"/>.
/// </summary>
public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUsersQueryValidator"/> class.
    /// </summary>
    public GetUsersQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());
    }
}
