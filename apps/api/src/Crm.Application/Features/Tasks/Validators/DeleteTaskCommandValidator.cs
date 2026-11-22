using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteTaskCommand"/>.
/// </summary>
public sealed class DeleteTaskCommandValidator : AbstractValidator<DeleteTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskCommandValidator"/> class.
    /// </summary>
    public DeleteTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);
    }
}
