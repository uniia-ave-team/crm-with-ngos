using System.Text.Json.Serialization;
using Crm.Application.Common.Interfaces;
using Crm.Domain.Enums;
using Mediator;

namespace Crm.Application.Features.Users.Commands;

/// <summary>
/// Represents a command to update the profile details of an existing user.
/// </summary>
/// <param name="FirstName">The updated first name.</param>
/// <param name="LastName">The updated last name.</param>
/// <param name="Patronymic">The updated patronymic.</param>
/// <param name="PhoneNumber">The updated phone number.</param>
/// <param name="Country">The updated country of residence.</param>
/// <param name="EmergencyContact">The updated emergency contact information.</param>
/// <param name="PreferredLanguage">The updated preferred interface language.</param>
/// <param name="Pronouns">The updated preferred pronouns.</param>
/// <param name="PronounCategory">The updated grammatical category of pronouns.</param>
public record UpdateUserProfileCommand(
    string FirstName,
    string LastName,
    string? Patronymic,
    string? PhoneNumber,
    string? Country,
    string? EmergencyContact,
    string? PreferredLanguage,
    string? Pronouns,
    PronounCategory? PronounCategory) : ICommand, ITransactionalCommand, ICurrentUserAware, ICurrentUserRolesAware, ISelfTargetableUserRequest
{
    /// <summary>
    /// Gets or sets the unique identifier of the user to update, populated from the route or request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; set; }

    /// <inheritdoc />
    [JsonIgnore]
    public bool TargetCurrentUser { get; init; }

    /// <summary>
    /// Gets or sets the unique identifier of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public Guid CurrentUserId { get; set; }

    /// <summary>
    /// Gets or sets the role identifiers of the currently authenticated user, populated by the request pipeline.
    /// </summary>
    [JsonIgnore]
    public List<Guid> CurrentUserRoleIds { get; set; } = [];
}
