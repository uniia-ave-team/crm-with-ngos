using Crm.Application.Features.Users.Commands;
using Crm.Domain.Constants;
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
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Original file name is required.")
            .Must(fileName => !fileName.Contains('\0'))
            .WithMessage("Original file name contains invalid characters.")
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
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('\0'))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) &&
               FileStorageConstants.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
