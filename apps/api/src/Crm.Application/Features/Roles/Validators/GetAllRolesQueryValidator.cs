using Crm.Application.Dtos.Role.Queries;
using Crm.Domain.Consts;
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
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be greater than or equal to 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => sortOrder is SortOrderConstants.Ascending or SortOrderConstants.Descending)
            .WithMessage("Sort order must be either 'asc' or 'desc'.")
            .When(x => !string.IsNullOrEmpty(x.SortOrder));
    }
}
