using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Queries;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetTaskByIdQuery"/>.
/// </summary>
public sealed class GetTaskByIdQueryValidator : AbstractValidator<GetTaskByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskByIdQueryValidator"/> class.
    /// </summary>
    public GetTaskByIdQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);
    }
}
