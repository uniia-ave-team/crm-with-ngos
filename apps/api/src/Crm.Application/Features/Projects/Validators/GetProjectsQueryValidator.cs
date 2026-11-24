using Crm.Application.Common.Validators;
using Crm.Application.Features.Projects.Queries;
using FluentValidation;

namespace Crm.Application.Features.Projects.Validators;

/// <summary>
/// Provides validation rules for <see cref="GetProjectsQuery"/>.
/// </summary>
public sealed class GetProjectsQueryValidator : AbstractValidator<GetProjectsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetProjectsQueryValidator"/> class.
    /// </summary>
    public GetProjectsQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());
    }
}
