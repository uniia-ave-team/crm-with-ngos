using Crm.Application.Features.Users.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUserProfileQuery"/>.
/// </summary>
public sealed class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserProfileQueryValidator"/> class.
    /// </summary>
    public GetUserProfileQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
