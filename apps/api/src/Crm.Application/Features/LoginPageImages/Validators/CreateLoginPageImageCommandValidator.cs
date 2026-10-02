using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Domain.Consts.Entities;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="CreateLoginPageImageCommand"/>.
/// </summary>
public class CreateLoginPageImageCommandValidator : AbstractValidator<CreateLoginPageImageCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateLoginPageImageCommandValidator"/> class.
    /// </summary>
    public CreateLoginPageImageCommandValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("Image URL is required.")
            .MaximumLength(LoginPageImageValidationConstants.MaxImageUrlLength)
                .WithMessage($"Image URL must not exceed {LoginPageImageValidationConstants.MaxImageUrlLength} characters.")
            .Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                .WithMessage("Image URL must be a valid absolute URI.");
    }
}
