using Crm.Application.Common.Consts;
using Crm.Application.Features.Projects.Commands;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteTopicCommand"/>.
/// </summary>
public sealed class DeleteTopicCommandValidator : AbstractValidator<DeleteTopicCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTopicCommandValidator"/> class.
    /// </summary>
    public DeleteTopicCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TopicId)
            .NotEmpty().WithMessage(ValidationMessages.TopicIdRequired);
    }
}
