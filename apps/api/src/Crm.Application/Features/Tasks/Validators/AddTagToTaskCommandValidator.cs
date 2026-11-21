using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="AddTagToTaskCommand"/>.
/// </summary>
public class AddTagToTaskCommandValidator : AbstractValidator<AddTagToTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddTagToTaskCommandValidator"/> class.
    /// </summary>
    public AddTagToTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired);
    }
}
