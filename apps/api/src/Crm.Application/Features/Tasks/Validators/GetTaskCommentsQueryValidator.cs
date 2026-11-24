using Crm.Application.Common.Consts;
using Crm.Application.Features.Tasks.Queries;
using Crm.Domain.Constants;
using FluentValidation;

namespace Crm.Application.Features.Tasks.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetTaskCommentsQuery"/>.
/// </summary>
public sealed class GetTaskCommentsQueryValidator : AbstractValidator<GetTaskCommentsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTaskCommentsQueryValidator"/> class.
    /// </summary>
    public GetTaskCommentsQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        RuleFor(x => x.TaskId)
            .NotEmpty().WithMessage(ValidationMessages.TaskIdRequired);

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(PaginationConstants.MinPageNumber)
            .WithMessage($"Page number must be greater than or equal to {PaginationConstants.MinPageNumber}.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(PaginationConstants.MinPageSize, PaginationConstants.MaxPageSize)
            .WithMessage($"Page size must be between {PaginationConstants.MinPageSize} and {PaginationConstants.MaxPageSize}.");
    }
}
