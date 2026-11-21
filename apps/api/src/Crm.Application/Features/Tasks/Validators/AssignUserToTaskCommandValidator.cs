using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="AssignUserToTaskCommand"/>.
/// </summary>
public class AssignUserToTaskCommandValidator : AbstractValidator<AssignUserToTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssignUserToTaskCommandValidator"/> class.
    /// </summary>
    public AssignUserToTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired);
    }
}
