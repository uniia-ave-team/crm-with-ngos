namespace Crm.Domain.Consts;

/// <summary>
/// Constants representing custom claim types used within the application's JWT tokens.
/// </summary>
public static class CustomClaimTypes
{
    public const string InviteType = "invite_type";
    public const string NgoId = "ngo_id";
    public const string RoleId = "role_id";
    public const string RegistrationInviteValue = "registration";
}
