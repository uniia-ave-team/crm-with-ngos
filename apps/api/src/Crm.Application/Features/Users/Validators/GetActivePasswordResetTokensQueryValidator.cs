using Crm.Application.Common.Validators;
using Crm.Application.Features.Users.Queries;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetActivePasswordResetTokensQuery"/>.
/// </summary>
public sealed class GetActivePasswordResetTokensQueryValidator : AbstractValidator<GetActivePasswordResetTokensQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetActivePasswordResetTokensQueryValidator"/> class.
    /// </summary>
    public GetActivePasswordResetTokensQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(UserActionTokenExtensions.IsAllowedPasswordResetOrderBy)
            .WithMessage(x => $"OrderBy property '{x.OrderBy}' is invalid. Allowed values are: Email, FirstName, LastName, FullName, CreatedAt, ExpiresAt.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("User ID must not be empty.")
            .When(x => x.UserId.HasValue);

        RuleFor(x => x.CreatedBy)
            .NotEqual(Guid.Empty)
            .WithMessage("CreatedBy user ID must not be empty.")
            .When(x => x.CreatedBy.HasValue);
    }
}
