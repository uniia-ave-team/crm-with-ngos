using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Domain.Consts;
using FluentValidation;

namespace Crm.Application.Features.LoginPageImages.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadLoginPageImageCommand"/>.
/// </summary>
public class UploadLoginPageImageCommandValidator : AbstractValidator<UploadLoginPageImageCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadLoginPageImageCommandValidator"/> class.
    /// </summary>
    public UploadLoginPageImageCommandValidator()
    {
        RuleFor(x => x.OriginalFileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Original file name is required.")
            .Must(IsValidExtension)
            .WithMessage($"Invalid file extension. Allowed extensions are: {string.Join(", ", FileStorageConstants.AllowedExtensions)}.");

        RuleFor(x => x.ContentStream)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("File stream cannot be null.")
            .Must(stream => stream.CanRead && stream.Length > 0)
            .WithMessage("File stream cannot be empty.");
    }

    private static bool IsValidExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) &&
               FileStorageConstants.AllowedExtensions.Contains(extension.ToLowerInvariant());
    }
}
