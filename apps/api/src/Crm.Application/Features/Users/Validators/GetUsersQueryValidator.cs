using Crm.Application.Dtos.User.Queries;
using Crm.Domain.Consts;
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
