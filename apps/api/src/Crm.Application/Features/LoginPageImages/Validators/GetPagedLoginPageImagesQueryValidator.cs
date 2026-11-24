using Crm.Application.Common.Validators;
using Crm.Application.Features.LoginPageImages.Queries;
using Crm.Domain.Entities;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetPagedLoginPageImagesQuery"/>.
/// </summary>
public sealed class GetPagedLoginPageImagesQueryValidator : AbstractValidator<GetPagedLoginPageImagesQuery>
{
    private static readonly string[] AllowedOrderByFields =
    [
        nameof(LoginPageImage.Id),
        nameof(LoginPageImage.Url),
        nameof(LoginPageImage.CreatedAt),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="GetPagedLoginPageImagesQueryValidator"/> class.
    /// </summary>
    public GetPagedLoginPageImagesQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(orderBy => orderBy is not null && AllowedOrderByFields.Contains(orderBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"OrderBy must be one of the following: {string.Join(", ", AllowedOrderByFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
