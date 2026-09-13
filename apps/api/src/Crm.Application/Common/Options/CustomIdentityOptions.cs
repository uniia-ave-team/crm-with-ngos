using System.ComponentModel.DataAnnotations;

namespace Crm.Application.Common.Options;

public class CustomIdentityOptions
{
    public const string Position = "IdentityOptions";

    [Range(1, 100, ErrorMessage = "RequiredLength must be between 1 and 100.")]
    public int RequiredLength { get; set; } = 8;

    public bool RequireDigit { get; set; } = true;

    public bool RequireNonAlphanumeric { get; set; } = true;

    public bool RequireUppercase { get; set; } = true;

    public bool RequireLowercase { get; set; } = true;

    public bool RequireUniqueEmail { get; set; } = true;
}
