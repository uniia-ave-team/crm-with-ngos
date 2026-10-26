using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomRecords.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetAllCustomRecordsQuery"/>.
/// </summary>
public class GetAllCustomRecordsQueryValidator : AbstractValidator<GetAllCustomRecordsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllCustomRecordsQueryValidator"/> class.
    /// </summary>
    public GetAllCustomRecordsQueryValidator()
    {
        RuleFor(x => x.CustomTableId)
            .NotEmpty().WithMessage("Custom table ID is required.");

        Include(new PagedSearchQueryValidationRules());
    }
}
