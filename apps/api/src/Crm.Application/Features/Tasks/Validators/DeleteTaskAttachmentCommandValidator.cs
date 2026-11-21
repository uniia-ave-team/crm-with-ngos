using Crm.Application.Common.Consts;
using Crm.Application.Dtos.Task.Commands;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="DeleteTaskAttachmentCommand"/>.
/// </summary>
public class DeleteTaskAttachmentCommandValidator : AbstractValidator<DeleteTaskAttachmentCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTaskAttachmentCommandValidator"/> class.
    /// </summary>
    public DeleteTaskAttachmentCommandValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.AttachmentId)
            .NotEmpty().WithMessage(ValidationMessages.AttachmentIdRequired);
    }
}
