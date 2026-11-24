using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Queries;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetTaskAttachmentsQuery"/>.
/// </summary>
public sealed class GetTaskAttachmentsQueryValidator : AbstractValidator<GetTaskAttachmentsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskAttachmentsQueryValidator"/> class.
    /// </summary>
    public GetTaskAttachmentsQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);
    }
}
