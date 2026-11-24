using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTaskCommand"/>.
/// </summary>
public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
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
            .Must((command, endDate) => endDate!.Value.ToUtc() >= command.StartDate.ToUtc())
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.EndDate.HasValue);

        RuleFor(x => x.ExternalLinks)
            .ValidTaskExternalLinks();

        RuleFor(x => x.Metadata)
            .ValidTaskMetadata();

        RuleForEach(x => x.AssigneeIds)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired)
            .When(x => x.AssigneeIds is not null);

        RuleForEach(x => x.TagIds)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired)
            .When(x => x.TagIds is not null);
    }
}
