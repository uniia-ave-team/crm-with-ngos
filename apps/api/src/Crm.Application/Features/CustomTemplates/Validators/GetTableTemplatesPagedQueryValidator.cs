using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTemplates.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetTableTemplatesPagedQuery"/>.
/// </summary>
public class GetTableTemplatesPagedQueryValidator : AbstractValidator<GetTableTemplatesPagedQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetTableTemplatesPagedQueryValidator"/> class.
    /// </summary>
    public GetTableTemplatesPagedQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());
    }
}
