using Crm.Application.Dtos.LoginPageImage.Queries;
using Crm.Domain.Consts;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetPagedLoginPageImagesQuery"/>.
/// </summary>
public class GetPagedLoginPageImagesQueryValidator : AbstractValidator<GetPagedLoginPageImagesQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetPagedLoginPageImagesQueryValidator"/> class.
    /// </summary>
    public GetPagedLoginPageImagesQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(PaginationConstants.MinPageNumber).WithMessage($"Page number must be greater than or equal to {PaginationConstants.MinPageNumber}.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(PaginationConstants.MinPageSize, PaginationConstants.MaxPageSize).WithMessage($"Page size must be between {PaginationConstants.MinPageSize} and {PaginationConstants.MaxPageSize}.");

        RuleFor(x => x.SortOrder)
            .Must(sortOrder => sortOrder is SortOrderConstants.Ascending or SortOrderConstants.Descending)
            .WithMessage("Sort order must be either 'asc' or 'desc'.")
            .When(x => !string.IsNullOrEmpty(x.SortOrder));
    }
}
