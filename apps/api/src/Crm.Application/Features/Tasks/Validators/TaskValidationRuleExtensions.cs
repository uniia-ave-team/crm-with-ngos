using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task;
using Crm.Domain.Consts.Entities.ValidationConstants;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides shared FluentValidation rule builder extensions for task and task comment commands.
/// </summary>
internal static class TaskValidationRuleExtensions
{
    private const int RootMetadataDepth = 1;

    /// <summary>
    /// Applies standard validation rules for a task title.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the task title property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ValidTaskTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.TaskTitleRequired)
            .MaximumLength(TaskValidationConstants.MaxTitleLength)
            .WithMessage(ValidationMessages.TaskTitleMaxLength);

    /// <summary>
    /// Applies standard validation rules for an optional task description.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the task description property.</param>
    /// <param name="selector">A function retrieving the description value from the command.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> ValidTaskDescription<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        Func<T, string?> selector)
        => ruleBuilder
            .MaximumLength(TaskValidationConstants.MaxDescriptionLength)
            .WithMessage(ValidationMessages.TaskDescriptionMaxLength)
            .When(x => !string.IsNullOrEmpty(selector(x)));

    /// <summary>
    /// Applies standard validation rules for a task comment content.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the comment content property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ValidTaskCommentContent<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.CommentContentRequired)
            .MaximumLength(TaskValidationConstants.MaxCommentContentLength)
            .WithMessage(ValidationMessages.CommentContentMaxLength);

    /// <summary>
    /// Applies validation rules for an optional collection of external links attached to a task.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the external links collection.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, IReadOnlyList<TaskExternalLinkDto>?> ValidTaskExternalLinks<T>(
        this IRuleBuilder<T, IReadOnlyList<TaskExternalLinkDto>?> ruleBuilder)
        => ruleBuilder
            .Must(links => links is null || links.Count <= TaskValidationConstants.MaxExternalLinksCount)
            .WithMessage(ValidationMessages.ExternalLinksMaxCount)
            .Must(links => links is null || links.All(IsValidExternalLink))
            .WithMessage(ValidationMessages.ExternalLinkInvalid);

    /// <summary>
    /// Applies validation rules for an optional JSON metadata payload attached to a task.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the metadata JSON object.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, JsonObject?> ValidTaskMetadata<T>(
        this IRuleBuilder<T, JsonObject?> ruleBuilder)
        => ruleBuilder
            .Must(metadata => metadata is null || metadata.Count <= TaskValidationConstants.MaxMetadataProperties)
            .WithMessage(ValidationMessages.MetadataMaxProperties)
            .Must(metadata => metadata is null || IsValidMetadataStructure(metadata, RootMetadataDepth))
            .WithMessage(ValidationMessages.MetadataInvalidStructure)
            .Must(metadata => metadata is null || metadata.ToJsonString().Length <= TaskValidationConstants.MaxMetadataSerializedChars)
            .WithMessage(ValidationMessages.MetadataMaxSerializedChars);

    private static bool IsValidExternalLink(TaskExternalLinkDto? link)
    {
        if (link is null || string.IsNullOrWhiteSpace(link.Url))
        {
            return false;
        }

        string trimmedUrl = link.Url.Trim();
        return trimmedUrl.Length <= TaskValidationConstants.MaxExternalLinkUrlLength
            && Uri.TryCreate(trimmedUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && (link.Title is null || link.Title.Trim().Length <= TaskValidationConstants.MaxExternalLinkTitleLength);
    }

    private static bool IsValidMetadataStructure(JsonNode? node, int currentDepth)
    {
        if (node is null)
        {
            return true;
        }

        if (currentDepth > TaskValidationConstants.MaxMetadataDepth)
        {
            return false;
        }

        switch (node)
        {
            case JsonObject obj:
                foreach (KeyValuePair<string, JsonNode?> entry in obj)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key)
                        || entry.Key.Length > TaskValidationConstants.MaxMetadataKeyLength)
                    {
                        return false;
                    }

                    if (entry.Value is JsonObject or JsonArray
                        && !IsValidMetadataStructure(entry.Value, currentDepth + 1))
                    {
                        return false;
                    }
                }

                return true;

            case JsonArray array:
                foreach (JsonNode? item in array)
                {
                    if (item is JsonObject or JsonArray
                        && !IsValidMetadataStructure(item, currentDepth + 1))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return true;
        }
    }
}
