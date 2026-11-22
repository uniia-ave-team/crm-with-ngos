using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="MarkTaskCommentsReadCommand"/>.
/// </summary>
public sealed class MarkTaskCommentsReadCommandValidator : AbstractValidator<MarkTaskCommentsReadCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkTaskCommentsReadCommandValidator"/> class.
    /// </summary>
    public MarkTaskCommentsReadCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);
    }
}
