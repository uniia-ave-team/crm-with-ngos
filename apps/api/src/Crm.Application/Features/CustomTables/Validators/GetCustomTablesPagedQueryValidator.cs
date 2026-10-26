using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetCustomTablesPagedQuery"/>.
/// </summary>
public class GetCustomTablesPagedQueryValidator : AbstractValidator<GetCustomTablesPagedQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomTablesPagedQueryValidator"/> class.
    /// </summary>
    public GetCustomTablesPagedQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());
    }
}
