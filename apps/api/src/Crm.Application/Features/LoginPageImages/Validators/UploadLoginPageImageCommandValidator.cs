using Crm.Application.Common.Validators;
using Crm.Application.Features.LoginPageImages.Commands;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadLoginPageImageCommand"/>.
/// </summary>
public sealed class UploadLoginPageImageCommandValidator : AbstractValidator<UploadLoginPageImageCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadLoginPageImageCommandValidator"/> class.
    /// </summary>
    public UploadLoginPageImageCommandValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .ApplyImageFileNameRules();

        RuleFor(x => x.ContentStream)
            .ApplyNonEmptyStreamRules();
    }
}
