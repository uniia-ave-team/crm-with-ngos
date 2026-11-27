using Crm.Application.Common.Validators;
using Crm.Application.Features.Ngos.Commands;
using FluentValidation;

namespace Crm.Application.Features.Ngos.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadNgoLogoCommand"/>.
/// </summary>
public sealed class UploadNgoLogoCommandValidator : AbstractValidator<UploadNgoLogoCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadNgoLogoCommandValidator"/> class.
    /// </summary>
    public UploadNgoLogoCommandValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .ApplyImageFileNameRules();

        RuleFor(x => x.ContentStream)
            .ApplyNonEmptyStreamRules();
    }
}
