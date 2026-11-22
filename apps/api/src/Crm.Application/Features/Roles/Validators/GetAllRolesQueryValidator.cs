using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Roles;
using Crm.Application.Features.Roles.Queries;
using FluentValidation;

namespace Crm.Application.Features.Roles.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetAllRolesQuery"/>.
/// </summary>
public sealed class GetAllRolesQueryValidator : AbstractValidator<GetAllRolesQuery>
{
    private static readonly string[] AllowedOrderByFields =
    [
        nameof(RoleDto.Id),
        nameof(RoleDto.Name),
        nameof(RoleDto.FeminitiveName),
        nameof(RoleDto.PluralName),
    ];

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAllRolesQueryValidator"/> class.
    /// </summary>
    public GetAllRolesQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(orderBy => orderBy is not null && AllowedOrderByFields.Contains(orderBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"OrderBy must be one of the following: {string.Join(", ", AllowedOrderByFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
