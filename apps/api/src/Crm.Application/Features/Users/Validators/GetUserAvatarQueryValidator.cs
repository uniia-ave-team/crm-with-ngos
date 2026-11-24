using Crm.Application.Features.Users.Queries;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUserAvatarQuery"/>.
/// </summary>
public sealed class GetUserAvatarQueryValidator : AbstractValidator<GetUserAvatarQuery>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserAvatarQueryValidator"/> class.
    /// </summary>
    public GetUserAvatarQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
