using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UnassignUserFromTaskCommand"/>.
/// </summary>
public sealed class UnassignUserFromTaskCommandValidator : AbstractValidator<UnassignUserFromTaskCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnassignUserFromTaskCommandValidator"/> class.
    /// </summary>
    public UnassignUserFromTaskCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage(ValidationMessages.UserIdRequired);
    }
}
