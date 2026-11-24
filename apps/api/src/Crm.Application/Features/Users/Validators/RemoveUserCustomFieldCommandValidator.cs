using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="RemoveUserCustomFieldCommand"/>.
/// </summary>
public sealed class RemoveUserCustomFieldCommandValidator : AbstractValidator<RemoveUserCustomFieldCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveUserCustomFieldCommandValidator"/> class.
    /// </summary>
    public RemoveUserCustomFieldCommandValidator()
    {
        RuleFor(x => x.CustomFieldId)
            .NotEmpty().WithMessage("Custom field ID is required.");

        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");
    }
}
