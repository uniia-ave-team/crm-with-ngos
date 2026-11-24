using Crm.Application.Common.Validators;
using Crm.Application.Features.CustomRecords.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetCustomRecordsPagedQuery"/>.
/// </summary>
public sealed class GetCustomRecordsPagedQueryValidator : AbstractValidator<GetCustomRecordsPagedQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomRecordsPagedQueryValidator"/> class.
    /// </summary>
    public GetCustomRecordsPagedQueryValidator()
    {
        RuleFor(x => x.CustomTableId)
            .NotEmpty().WithMessage("Custom table ID is required.");

        Include(new PagedSearchQueryValidationRules());
    }
}
