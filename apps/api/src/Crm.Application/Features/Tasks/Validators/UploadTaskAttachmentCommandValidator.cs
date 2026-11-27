using System.Collections.Frozen;
using System.Net.Mime;
using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Application.Features.Tasks.Commands;
using Crm.Domain.Constants.Validation;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UploadTaskAttachmentCommand"/>,
/// enforcing file size limits, allow-listed extensions, and MIME content-type consistency.
/// </summary>
public sealed class UploadTaskAttachmentCommandValidator : AbstractValidator<UploadTaskAttachmentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UploadTaskAttachmentCommandValidator"/> class.
    /// </summary>
    public UploadTaskAttachmentCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.OriginalFileName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(ValidationMessages.AttachmentFileNameRequired)
            .NotContainNullChars()
            .MaximumLength(TaskValidationConstants.MaxAttachmentFileNameLength)
            .WithMessage(ValidationMessages.AttachmentFileNameMaxLength)
            .Must(HasAllowedExtension)
            .WithMessage(ValidationMessages.UnsupportedAttachmentExtension);

        RuleFor(x => x.ContentType)
            .MaximumLength(TaskValidationConstants.MaxAttachmentContentTypeLength)
            .NotContainNullChars()
            .When(x => !string.IsNullOrEmpty(x.ContentType));

        RuleFor(x => x)
            .Must(HasConsistentContentType)
            .WithName(nameof(UploadTaskAttachmentCommand.ContentType))
            .WithMessage(ValidationMessages.AttachmentContentTypeMismatch)
            .When(x => !string.IsNullOrWhiteSpace(x.OriginalFileName) && HasAllowedExtension(x.OriginalFileName));

        RuleFor(x => x.ContentStream)
            .ApplyNonEmptyStreamRules(
                ValidationMessages.AttachmentStreamNull,
                ValidationMessages.AttachmentStreamEmpty);

        RuleFor(x => x.FileSize)
            .GreaterThan(0).WithMessage(ValidationMessages.AttachmentFileEmpty)
            .LessThanOrEqualTo(TaskValidationConstants.MaxAttachmentFileSize)
            .WithMessage(ValidationMessages.AttachmentFileSizeMaxBytes);
    }

    private static bool HasAllowedExtension(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return false;
        }

        string extension = Path.GetExtension(originalFileName.ExtractFileName());
        return !string.IsNullOrEmpty(extension)
               && TaskValidationConstants.AllowedAttachmentExtensions.ContainsKey(extension);
    }

    private static bool HasConsistentContentType(UploadTaskAttachmentCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.ContentType))
        {
            return true;
        }

        string mediaType = NormalizeMediaType(command.ContentType);
        if (string.IsNullOrEmpty(mediaType)
            || string.Equals(mediaType, MediaTypeNames.Application.Octet, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string extension = Path.GetExtension(command.OriginalFileName.ExtractFileName());
        return TaskValidationConstants.AllowedAttachmentExtensions.TryGetValue(extension, out FrozenSet<string>? allowedTypes)
               && allowedTypes.Contains(mediaType);
    }

    private static string NormalizeMediaType(string contentType)
    {
        int semicolonIndex = contentType.IndexOf(';');
        string raw = semicolonIndex >= 0 ? contentType[..semicolonIndex] : contentType;
        return raw.Trim();
    }
}
