using Crm.Application.Dtos.User.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUserProfileQuery"/>.
/// </summary>
public class GetUserProfileQueryValidator : AbstractValidator<GetUserProfileQuery>
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
