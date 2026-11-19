using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTaskCommand"/>.
/// </summary>
public class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskCommandValidator"/> class.
    /// </summary>
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.TopicId)
            .NotEmpty().WithMessage(ValidationMessages.TopicIdRequired);

        RuleFor(x => x.Title)
            .ValidTaskTitle();

        RuleFor(x => x.Description)
            .ValidTaskDescription(x => x.Description);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskStatus);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskPriority);

        RuleFor(x => x.StartDate)
            .NotEmpty().WithMessage(ValidationMessages.StartDateRequired);

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.ExternalLinks)
            .ValidTaskExternalLinks();

        RuleFor(x => x.Metadata)
            .ValidTaskMetadata();
    }
}
