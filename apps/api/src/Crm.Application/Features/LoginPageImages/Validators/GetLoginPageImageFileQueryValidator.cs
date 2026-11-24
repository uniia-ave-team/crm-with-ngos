using Crm.Application.Features.LoginPageImages.Queries;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetLoginPageImageFileQuery"/>.
/// </summary>
public sealed class GetLoginPageImageFileQueryValidator : AbstractValidator<GetLoginPageImageFileQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetLoginPageImageFileQueryValidator"/> class.
    /// </summary>
    public GetLoginPageImageFileQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Image ID is required.");
    }
}
