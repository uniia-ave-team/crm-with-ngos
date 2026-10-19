namespace Crm.Application.Dtos.Common;

/// <summary>
/// Data transfer object containing identity system settings and password complexity requirements.
/// </summary>
public record IdentityOptionsDto(
    int RequiredLength,
    bool RequireDigit,
    bool RequireNonAlphanumeric,
    bool RequireUppercase,
    bool RequireLowercase,
    bool RequireUniqueEmail);
