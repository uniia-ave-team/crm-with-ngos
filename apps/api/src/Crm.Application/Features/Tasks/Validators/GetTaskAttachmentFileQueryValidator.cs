using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Queries;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetTaskAttachmentFileQuery"/>.
/// </summary>
public sealed class GetTaskAttachmentFileQueryValidator : AbstractValidator<GetTaskAttachmentFileQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskAttachmentFileQueryValidator"/> class.
    /// </summary>
    public GetTaskAttachmentFileQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.AttachmentId)
            .NotEmpty().WithMessage(ValidationMessages.AttachmentIdRequired);
    }
}
