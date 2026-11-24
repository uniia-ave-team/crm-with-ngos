using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTopicCommand"/>.
/// </summary>
public sealed class UpdateTopicCommandValidator : AbstractValidator<UpdateTopicCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTopicCommandValidator"/> class.
    /// </summary>
    public UpdateTopicCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TopicId)
            .NotEmpty().WithMessage(ValidationMessages.TopicIdRequired);

        RuleFor(x => x.Title)
            .ValidTopicTitle();
    }
}
