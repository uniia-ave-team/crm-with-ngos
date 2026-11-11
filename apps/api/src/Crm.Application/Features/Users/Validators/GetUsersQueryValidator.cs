using Crm.Application.Common.Validators;
using Crm.Application.Dtos.User;
using Crm.Application.Dtos.User.Queries;
using Crm.Domain.Consts.Entities.GeneralConstants;
using Crm.Domain.Entities;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides validation rules for the <see cref="GetUsersQuery"/>.
/// </summary>
public class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    // TODO: Support dynamic sorting by custom field keys with DataType resolution (Date, Number, Text) once UserCustomField has DataType support.
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(UserDto.Id),
        nameof(UserDto.Email),
        nameof(UserDto.FirstName),
        nameof(UserDto.LastName),
        nameof(UserDto.Patronymic),
        nameof(UserDto.PhoneNumber),
        nameof(UserDto.Country),
        nameof(UserDto.EmergencyContact),
        nameof(UserDto.Pronouns),
        nameof(UserDto.PronounCategory),
        nameof(UserDto.IsActive),
        UserConstants.FullName,
        nameof(User.CreatedAt),
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUsersQueryValidator"/> class.
    /// </summary>
    public GetUsersQueryValidator()
    {
        Include(new PagedSearchQueryValidationRules());

        RuleFor(x => x.OrderBy)
            .Must(orderBy => orderBy == null || AllowedSortFields.Contains(orderBy.Trim()))
            .WithMessage(x => $"OrderBy property '{x.OrderBy}' is invalid. Allowed sort fields are: {string.Join(", ", AllowedSortFields)}.")
            .When(x => !string.IsNullOrWhiteSpace(x.OrderBy));
    }
}
