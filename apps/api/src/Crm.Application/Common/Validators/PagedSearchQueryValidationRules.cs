using Crm.Application.Common.Consts;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Constants;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides validation rules for queries implementing <see cref="IPagedSearchQuery"/>.
/// </summary>
public sealed class PagedSearchQueryValidationRules : AbstractValidator<IPagedSearchQuery>
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

        RuleFor(x => x.OrderBy)
            .MaximumLength(SearchValidationConstants.MaxSearchTermLength)
            .WithMessage($"Order by field must not exceed {SearchValidationConstants.MaxSearchTermLength} characters.")
            .Must(orderBy => orderBy is null || !orderBy.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => !string.IsNullOrEmpty(x.OrderBy));

        RuleFor(x => x.SortOrder)
            .Must(IsValidSortOrder)
            .WithMessage($"Sort order must be either '{SortOrderConstants.Ascending}' or '{SortOrderConstants.Descending}'.")
            .When(x => !string.IsNullOrEmpty(x.SortOrder));

        RuleFor(x => x.SearchTerm)
            .MaximumLength(SearchValidationConstants.MaxSearchTermLength)
            .WithMessage($"Search term must not exceed {SearchValidationConstants.MaxSearchTermLength} characters.")
            .Must(term => term is null || !term.Contains('\0', StringComparison.Ordinal))
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .When(x => x.SearchTerm is not null);

        RuleFor(x => x.SearchTerm)
            .Must(term => term!.Trim().Length >= SearchValidationConstants.MinSearchTermLength)
            .WithMessage($"Search term must be at least {SearchValidationConstants.MinSearchTermLength} characters long.")
            .When(x => !string.IsNullOrWhiteSpace(x.SearchTerm));
    }

    private static bool IsValidSortOrder(string? sortOrder)
    {
        if (string.IsNullOrWhiteSpace(sortOrder))
        {
            return false;
        }

        string trimmed = sortOrder.Trim();
        return string.Equals(trimmed, SortOrderConstants.Ascending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, SortOrderConstants.Descending, StringComparison.OrdinalIgnoreCase);
    }
}
