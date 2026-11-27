using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides shared FluentValidation rule builder extensions for project, member, tag, and topic commands.
/// </summary>
internal static class ProjectValidationRuleExtensions
{
    /// <summary>
    /// Applies standard validation rules for a project title.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the project title property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ValidProjectTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.ProjectTitleRequired)
            .MaximumLength(ProjectValidationConstants.MaxTitleLength)
            .WithMessage(ValidationMessages.ProjectTitleMaxLength)
            .NotContainNullChars()!;

    /// <summary>
    /// Applies standard validation rules for an optional project description.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the project description property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> ValidProjectDescription<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder.ApplyOptionalTextRules(
            ProjectValidationConstants.MaxDescriptionLength,
            ValidationMessages.ProjectDescriptionMaxLength);

    /// <summary>
    /// Applies standard validation rules for a project member's optional custom functional role title.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the role title property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> ValidProjectRoleTitle<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
        => ruleBuilder.ApplyOptionalTextRules(
            ProjectValidationConstants.MaxRoleTitleLength,
            ValidationMessages.ProjectRoleTitleMaxLength);

    /// <summary>
    /// Applies standard validation rules for a project-scoped tag title.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the tag title property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ValidTagTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.TagTitleRequired)
            .MaximumLength(ProjectValidationConstants.MaxTagTitleLength)
            .WithMessage(ValidationMessages.TagTitleMaxLength)
            .NotContainNullChars()!;

    /// <summary>
    /// Applies standard validation rules for a board topic title.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The rule builder for the topic title property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ValidTopicTitle<T>(this IRuleBuilder<T, string> ruleBuilder)
        => ruleBuilder
            .NotEmpty().WithMessage(ValidationMessages.TopicTitleRequired)
            .MaximumLength(ProjectValidationConstants.MaxTopicTitleLength)
            .WithMessage(ValidationMessages.TopicTitleMaxLength)
            .NotContainNullChars()!;
}
