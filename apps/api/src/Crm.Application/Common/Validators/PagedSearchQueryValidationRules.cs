using Crm.Application.Common.Interfaces;
using Crm.Domain.Consts;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for queries implementing <see cref="IPagedSearchQuery"/>.
/// </summary>
public class PagedSearchQueryValidationRules : AbstractValidator<IPagedSearchQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PagedSearchQueryValidationRules"/> class.
    /// </summary>
    public PagedSearchQueryValidationRules()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(PaginationConstants.MinPageNumber)
            .WithMessage($"Page number must be greater than or equal to {PaginationConstants.MinPageNumber}.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(PaginationConstants.MinPageSize, PaginationConstants.MaxPageSize)
            .WithMessage($"Page size must be between {PaginationConstants.MinPageSize} and {PaginationConstants.MaxPageSize}.");

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => sortOrder is SortOrderConstants.Ascending or SortOrderConstants.Descending)
            .WithMessage($"Sort order must be either '{SortOrderConstants.Ascending}' or '{SortOrderConstants.Descending}'.")
            .When(x => !string.IsNullOrEmpty(x.SortOrder));

        RuleFor(x => x.SearchTerm)
            .MinimumLength(SearchValidationConstants.MinSearchTermLength)
            .WithMessage($"Search term must be at least {SearchValidationConstants.MinSearchTermLength} characters long.")
            .MaximumLength(SearchValidationConstants.MaxSearchTermLength)
            .WithMessage($"Search term must not exceed {SearchValidationConstants.MaxSearchTermLength} characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));
    }
}
