using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Task.Queries;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectTasksQuery"/>.
/// </summary>
public class GetProjectTasksQueryValidator : AbstractValidator<GetProjectTasksQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectTasksQueryValidator"/> class.
    /// </summary>
    public GetProjectTasksQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .Must(id => id != Guid.Empty).WithMessage(ValidationMessages.ProjectIdCannotBeEmpty)
            .When(x => x.ProjectId.HasValue);

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskStatus)
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskPriority)
            .When(x => x.Priority.HasValue);

        Include(new PagedSearchQueryValidationRules());
    }
}
