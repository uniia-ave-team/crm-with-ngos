using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteTaskCommentCommand"/>.
/// </summary>
public sealed class DeleteTaskCommentCommandValidator : AbstractValidator<DeleteTaskCommentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskCommentCommandValidator"/> class.
    /// </summary>
    public DeleteTaskCommentCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage(ValidationMessages.CommentIdRequired);
    }
}
