using Crm.Application.Common.Validators;
using Crm.Application.Dtos.TableTemplates;
using Crm.Application.Features.TableTemplates.Queries;
using FluentValidation;

namespace Crm.Application.Features.TableTemplates.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetTableTemplatesPagedQuery"/>.
/// </summary>
public sealed class GetTableTemplatesPagedQueryValidator : AbstractValidator<GetTableTemplatesPagedQuery>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(TableTemplateDto.Id),
        nameof(TableTemplateDto.Name),
        nameof(TableTemplateDto.Description),
        nameof(TableTemplateDto.IsSystemPreset),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTableTemplatesPagedQueryValidator"/> class.
    /// </summary>
    public GetTableTemplatesPagedQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(orderBy => AllowedSortFields.Contains((orderBy ?? string.Empty).Trim()))
            .WithMessage($"OrderBy must be one of the following: {string.Join(", ", AllowedSortFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
