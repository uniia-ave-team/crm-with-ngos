using Crm.Domain.Consts;
using Crm.Domain.Consts.Entities.ValidationConstants;

namespace Crm.Application.Common.Consts;

/// <summary>
/// Provides shared FluentValidation error message constants for projects, topics, tags, tasks, comments, and attachments.
/// </summary>
public static class ValidationMessages
{
    public const string ProjectIdRequired = "Project ID is required.";
    public const string ProjectIdCannotBeEmpty = "Project ID cannot be empty.";
    public const string TaskIdRequired = "Task ID is required.";
    public const string TopicIdRequired = "Topic ID is required.";
    public const string TopicIdCannotBeEmpty = "Topic ID cannot be empty.";
    public const string TagIdRequired = "Tag ID is required.";
    public const string UserIdRequired = "User ID is required.";
    public const string CommentIdRequired = "Comment ID is required.";
    public const string AttachmentIdRequired = "Attachment ID is required.";

    public const string ProjectTitleRequired = "Project title is required.";
    public const string InvalidProjectVisibility = "Invalid project visibility value.";
    public const string InvalidProjectClaim = "Invalid project claim value.";
    public const string TagTitleRequired = "Tag title is required.";
    public const string TopicTitleRequired = "Topic title is required.";

    public const string TaskTitleRequired = "Task title is required.";
    public const string InvalidTaskStatus = "Invalid task status.";
    public const string InvalidTaskPriority = "Invalid task priority.";
    public const string StartDateRequired = "Start date is required.";
    public const string EndDateBeforeStartDate = DomainErrorMessages.EndDateBeforeStartDate;
    public const string CommentContentRequired = "Comment content is required.";

    public const string AttachmentFileNameRequired = "File name is required.";
    public const string UnsupportedAttachmentExtension = "Unsupported or disallowed file extension.";
    public const string AttachmentContentTypeMismatch = "File content type does not match the allowed MIME types for the uploaded file extension.";
    public const string AttachmentStreamNull = "File stream cannot be null.";
    public const string AttachmentStreamEmpty = "File stream cannot be empty.";
    public const string AttachmentFileEmpty = "Uploaded file cannot be empty.";

    public static readonly string ProjectTitleMaxLength =
        $"Project title must not exceed {ProjectValidationConstants.MaxTitleLength} characters.";

    public static readonly string ProjectDescriptionMaxLength =
        $"Project description must not exceed {ProjectValidationConstants.MaxDescriptionLength} characters.";

    public static readonly string ProjectRoleTitleMaxLength =
        $"Role title must not exceed {ProjectValidationConstants.MaxRoleTitleLength} characters.";

    public static readonly string TagTitleMaxLength =
        $"Tag title must not exceed {ProjectValidationConstants.MaxTagTitleLength} characters.";

    public static readonly string TopicTitleMaxLength =
        $"Topic title must not exceed {ProjectValidationConstants.MaxTopicTitleLength} characters.";

    public static readonly string TaskTitleMaxLength =
        $"Task title must not exceed {TaskValidationConstants.MaxTitleLength} characters.";

    public static readonly string TaskDescriptionMaxLength =
        $"Task description must not exceed {TaskValidationConstants.MaxDescriptionLength} characters.";

    public static readonly string CommentContentMaxLength =
        $"Comment content must not exceed {TaskValidationConstants.MaxCommentContentLength} characters.";

    public static readonly string ExternalLinksMaxCount =
        $"Task cannot have more than {TaskValidationConstants.MaxExternalLinksCount} external links.";

    public static readonly string ExternalLinkInvalid =
        $"Each external link must have a valid HTTP or HTTPS URL (up to {TaskValidationConstants.MaxExternalLinkUrlLength} characters) and an optional title (up to {TaskValidationConstants.MaxExternalLinkTitleLength} characters).";

    public static readonly string MetadataMaxProperties =
        $"Task metadata cannot contain more than {TaskValidationConstants.MaxMetadataProperties} properties.";

    public static readonly string MetadataInvalidStructure =
        $"Task metadata keys must be non-empty (up to {TaskValidationConstants.MaxMetadataKeyLength} characters) and nesting depth must not exceed {TaskValidationConstants.MaxMetadataDepth}.";

    public static readonly string MetadataMaxSerializedChars =
        $"Task metadata serialized size must not exceed {TaskValidationConstants.MaxMetadataSerializedChars} characters.";

    public static readonly string AttachmentFileNameMaxLength =
        $"File name must not exceed {TaskValidationConstants.MaxAttachmentFileNameLength} characters.";

    public static readonly string AttachmentFileSizeMaxBytes =
        $"File size must not exceed {TaskValidationConstants.MaxAttachmentFileSize} bytes.";
}
