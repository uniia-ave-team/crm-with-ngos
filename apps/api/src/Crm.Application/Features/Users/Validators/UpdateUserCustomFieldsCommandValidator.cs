using Crm.Application.Features.CustomRecords.Validators;
using Crm.Application.Features.Users.Commands;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for <see cref="UpdateUserCustomFieldsCommand"/>, reusing
/// shared custom-record JSONB payload validation rules.
/// </summary>
public sealed class UpdateUserCustomFieldsCommandValidator : AbstractValidator<UpdateUserCustomFieldsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserCustomFieldsCommandValidator"/> class.
    /// </summary>
    public UpdateUserCustomFieldsCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Data)
            .ApplyCustomRecordDataRules();
    }
}
