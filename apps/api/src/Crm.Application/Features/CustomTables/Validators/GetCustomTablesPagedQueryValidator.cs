using Crm.Application.Common.Validators;
using Crm.Application.Dtos.CustomTables;
using Crm.Application.Features.CustomTables.Queries;
using FluentValidation;

namespace Crm.Application.Features.CustomTables.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetCustomTablesPagedQuery"/>.
/// </summary>
public sealed class GetCustomTablesPagedQueryValidator : AbstractValidator<GetCustomTablesPagedQuery>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(CustomTableSummaryDto.Id),
        nameof(CustomTableSummaryDto.Name),
        nameof(CustomTableSummaryDto.Description),
        nameof(CustomTableSummaryDto.FieldsCount),
        nameof(CustomTableSummaryDto.RecordsCount),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCustomTablesPagedQueryValidator"/> class.
    /// </summary>
    public GetCustomTablesPagedQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(orderBy => AllowedSortFields.Contains((orderBy ?? string.Empty).Trim()))
            .WithMessage($"OrderBy must be one of the following: {string.Join(", ", AllowedSortFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
