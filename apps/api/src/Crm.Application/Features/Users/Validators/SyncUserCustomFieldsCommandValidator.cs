using Crm.Application.Common.Validators;
using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Commands;
using Crm.Domain.Extensions;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for <see cref="SyncUserCustomFieldsCommand"/>, reusing
/// <see cref="IdentifiableFieldListValidator{TField}"/> with user-specific reserved key checks.
/// </summary>
public sealed class SyncUserCustomFieldsCommandValidator : AbstractValidator<SyncUserCustomFieldsCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SyncUserCustomFieldsCommandValidator"/> class.
    /// </summary>
    public SyncUserCustomFieldsCommandValidator()
    {
        RuleFor(x => x.Fields)
            .SetValidator(new IdentifiableFieldListValidator<UpdateUserCustomFieldDto>(UserCustomFieldExtensions.IsReservedKey));
    }
}
