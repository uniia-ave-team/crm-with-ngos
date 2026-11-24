using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Application.Features.Projects.Queries;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectTagsQuery"/>.
/// </summary>
public sealed class GetProjectTagsQueryValidator : AbstractValidator<GetProjectTagsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectTagsQueryValidator"/> class.
    /// </summary>
    public GetProjectTagsQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        Include(new PagedSearchQueryValidationRules());
    }
}
