using Crm.Application.Dtos.LoginPageImage.Queries;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetLoginPageImageByIdQuery"/>.
/// </summary>
public class GetLoginPageImageByIdQueryValidator : AbstractValidator<GetLoginPageImageByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetLoginPageImageByIdQueryValidator"/> class.
    /// </summary>
    public GetLoginPageImageByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Login page image ID is required.");
    }
}
