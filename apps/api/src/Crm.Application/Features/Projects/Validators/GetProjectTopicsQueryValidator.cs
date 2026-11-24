using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Application.Features.Projects.Queries;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectTopicsQuery"/>.
/// </summary>
public sealed class GetProjectTopicsQueryValidator : AbstractValidator<GetProjectTopicsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectTopicsQueryValidator"/> class.
    /// </summary>
    public GetProjectTopicsQueryValidator()
    {
        RuleFor(x => x.ProjectId)
            .NotEmpty().WithMessage(ValidationMessages.ProjectIdRequired);

        Include(new PagedSearchQueryValidationRules());
    }
}
