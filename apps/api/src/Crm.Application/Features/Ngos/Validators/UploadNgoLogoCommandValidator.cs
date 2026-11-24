using Crm.Application.Common.Consts;
using Crm.Application.Features.Ngos.Commands;
using Crm.Domain.Constants;
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
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Original file name is required.")
            .Must(fileName => !fileName.Contains('\0')).WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .Must(IsValidExtension)
            .WithMessage($"Invalid file extension. Allowed extensions are: {string.Join(", ", FileStorageConstants.AllowedExtensions)}.");

        RuleFor(x => x.ContentStream)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("File stream cannot be null.")
            .Must(stream => stream is not null && stream.CanRead && stream.CanSeek && stream.Length > 0)
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
               FileStorageConstants.AllowedExtensions.Contains(extension);
    }
}
