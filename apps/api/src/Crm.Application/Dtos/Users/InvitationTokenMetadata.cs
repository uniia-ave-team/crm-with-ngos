namespace Crm.Application.Dtos.Users;

/// <summary>
/// Typed metadata payload encrypted inside a <see cref="Domain.Entities.UserActionToken"/>
/// of type <see cref="Domain.Enums.ActionTokenType.Invitation"/>.
/// </summary>
/// <param name="NgoId">The unique identifier of the NGO organization.</param>
/// <param name="RoleIds">The distinct role identifiers to assign upon registration.</param>
public sealed record InvitationTokenMetadata(
    Guid NgoId,
    IReadOnlyList<Guid> RoleIds);
