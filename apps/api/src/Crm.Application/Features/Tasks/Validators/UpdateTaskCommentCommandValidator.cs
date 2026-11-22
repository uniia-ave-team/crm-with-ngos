using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateTaskCommentCommand"/>.
/// </summary>
public sealed class UpdateTaskCommentCommandValidator : AbstractValidator<UpdateTaskCommentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTaskCommentCommandValidator"/> class.
    /// </summary>
    public UpdateTaskCommentCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.CommentId)
            .NotEmpty().WithMessage(ValidationMessages.CommentIdRequired);

        RuleFor(x => x.Content)
            .ValidTaskCommentContent();
    }
}
