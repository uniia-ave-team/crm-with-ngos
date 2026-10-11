using Crm.Application.Dtos.LoginPageImage.Commands;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadLoginPageImageCommand"/>.
/// </summary>
public class UploadLoginPageImageCommandValidator : AbstractValidator<UploadLoginPageImageCommand>
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

    /// <summary>
    /// Initializes a new instance of the <see cref="UploadLoginPageImageCommandValidator"/> class.
    /// </summary>
    public UploadLoginPageImageCommandValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .NotEmpty().WithMessage("Original file name is required.")
            .Must(fileName => AllowedExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant()))
                .WithMessage($"Invalid file extension. Allowed extensions are: {string.Join(", ", AllowedExtensions)}.");

        RuleFor(x => x.ContentStream)
            .NotNull().WithMessage("File stream cannot be null.")
            .Must(stream => stream.Length > 0).WithMessage("File stream cannot be empty.");
    }
}
