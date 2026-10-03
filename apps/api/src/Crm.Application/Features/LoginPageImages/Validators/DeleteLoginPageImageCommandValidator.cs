using Crm.Application.Dtos.LoginPageImage.Commands;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="DeleteLoginPageImageCommand"/>.
/// </summary>
public class DeleteLoginPageImageCommandValidator : AbstractValidator<DeleteLoginPageImageCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteLoginPageImageCommandValidator"/> class.
    /// </summary>
    public DeleteLoginPageImageCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Login page image ID is required.");
    }
}
