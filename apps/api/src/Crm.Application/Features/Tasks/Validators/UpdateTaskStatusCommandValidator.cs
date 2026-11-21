using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTaskStatusCommand"/>.
/// </summary>
public class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskStatusCommandValidator"/> class.
    /// </summary>
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskStatus);

        RuleFor(x => x.TopicId)
            .Must(id => id != Guid.Empty).WithMessage(ValidationMessages.TopicIdCannotBeEmpty)
            .When(x => x.TopicId.HasValue);
    }
}
