using Crm.Application.Common.Validators;
using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadUserAvatarCommand"/>.
/// </summary>
public sealed class UploadUserAvatarCommandValidator : AbstractValidator<UploadUserAvatarCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadUserAvatarCommandValidator"/> class.
    /// </summary>
    public UploadUserAvatarCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.OriginalFileName)
            .ApplyImageFileNameRules();

        RuleFor(x => x.ContentStream)
            .ApplyNonEmptyStreamRules();
    }
}
