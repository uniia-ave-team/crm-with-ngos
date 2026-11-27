using Crm.Application.Common.Validators;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUsersQuery"/>.
/// </summary>
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUsersQueryValidator"/> class.
    /// </summary>
    public GetUsersQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules(minSearchTermLength: 1));

        RuleFor(x => x.OrderBy)
            .Must(UserCustomFieldExtensions.IsAllowedUserOrderBy)
            .WithMessage(x => $"OrderBy property '{x.OrderBy}' is invalid. Provide a standard user sort field or a valid custom field ID.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
