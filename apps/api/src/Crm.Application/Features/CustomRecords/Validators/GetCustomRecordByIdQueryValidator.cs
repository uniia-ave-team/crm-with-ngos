using Crm.Application.Dtos.CustomRecords.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetCustomRecordByIdQuery"/>.
/// </summary>
public class GetCustomRecordByIdQueryValidator : AbstractValidator<GetCustomRecordByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomRecordByIdQueryValidator"/> class.
    /// </summary>
    public GetCustomRecordByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Custom record ID is required.");
    }
}
