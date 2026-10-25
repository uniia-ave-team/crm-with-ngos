using Crm.Application.Dtos.CustomTables.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetCustomTableByIdQuery"/>.
/// </summary>
public class GetCustomTableByIdQueryValidator : AbstractValidator<GetCustomTableByIdQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomTableByIdQueryValidator"/> class.
    /// </summary>
    public GetCustomTableByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Table ID is required.");
    }
}
