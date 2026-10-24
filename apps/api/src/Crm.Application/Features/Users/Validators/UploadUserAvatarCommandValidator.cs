using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Consts;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="UploadUserAvatarCommand"/>.
/// </summary>
public class UploadUserAvatarCommandValidator : AbstractValidator<UploadUserAvatarCommand>
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
