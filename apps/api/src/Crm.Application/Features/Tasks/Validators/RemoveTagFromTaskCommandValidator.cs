using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="RemoveTagFromTaskCommand"/>.
/// </summary>
public class RemoveTagFromTaskCommandValidator : AbstractValidator<RemoveTagFromTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveTagFromTaskCommandValidator"/> class.
    /// </summary>
    public RemoveTagFromTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.TagId)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired);
    }
}
