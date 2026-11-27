using Crm.Application.Common.Validators;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetActiveInvitationsQuery"/>.
/// </summary>
public sealed class GetActiveInvitationsQueryValidator : AbstractValidator<GetActiveInvitationsQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetActiveInvitationsQueryValidator"/> class.
    /// </summary>
    public GetActiveInvitationsQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(UserActionTokenExtensions.IsAllowedInvitationOrderBy)
            .WithMessage(x => $"OrderBy property '{x.OrderBy}' is invalid. Allowed values are: Email, CreatedAt, ExpiresAt.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));

        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedBy user ID must not be empty.")
            .When(x => x.CreatedBy.HasValue);
    }
}
