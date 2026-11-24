using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Application.Features.Tasks.Queries;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectTasksQuery"/>.
/// </summary>
public sealed class GetProjectTasksQueryValidator : AbstractValidator<GetProjectTasksQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectTasksQueryValidator"/> class.
    /// </summary>
    public GetProjectTasksQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .Must(id => id != Guid.Empty).WithMessage(ValidationMessages.ProjectIdCannotBeEmpty)
            .When(x => x.ProjectId.HasValue);

        RuleFor(x => x.TopicId)
            .Must(id => id != Guid.Empty).WithMessage(ValidationMessages.TopicIdCannotBeEmpty)
            .When(x => x.TopicId.HasValue);

        RuleFor(x => x.Filter)
            .Must(f => string.Equals(f!.Trim(), TaskFilterConstants.MineFilterValue, StringComparison.OrdinalIgnoreCase))
            .WithMessage($"Filter must be '{TaskFilterConstants.MineFilterValue}' when specified.")
            .When(x => !string.IsNullOrWhiteSpace(x.Filter));

        RuleFor(x => x.Status)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskStatus)
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Priority)
            .IsInEnum().WithMessage(ValidationMessages.InvalidTaskPriority)
            .When(x => x.Priority.HasValue);

        RuleForEach(x => x.TagIds)
            .NotEmpty().WithMessage(ValidationMessages.TagIdRequired)
            .When(x => x.TagIds is not null);

        RuleFor(x => x.StartDateTo)
            .Must((query, startDateTo) => startDateTo!.Value.ToUtc() >= query.StartDateFrom!.Value.ToUtc())
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.StartDateFrom.HasValue && x.StartDateTo.HasValue);

        RuleFor(x => x.EndDateTo)
            .Must((query, endDateTo) => endDateTo!.Value.ToUtc() >= query.EndDateFrom!.Value.ToUtc())
            .WithMessage(ValidationMessages.EndDateBeforeStartDate)
            .When(x => x.EndDateFrom.HasValue && x.EndDateTo.HasValue);

        Include(new PagedSearchQueryValidationRules());
    }
}
