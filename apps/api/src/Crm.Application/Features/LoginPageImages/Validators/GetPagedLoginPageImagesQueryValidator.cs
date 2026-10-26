using Crm.Application.Common.Validators;
using Crm.Application.Dtos.LoginPageImage.Queries;
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
        Include(new PagedSearchQueryValidationRules());
    }
}
