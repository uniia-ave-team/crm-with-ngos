namespace Crm.Application.Extensions;

/// <summary>
/// Provides extension methods for anonymizing Personally Identifiable Information (PII).
/// </summary>
public static class PrivacyExtensions
{
    /// <summary>
    /// Masks an email address for safe logging (e.g., "john.doe@gmail.com" becomes "j***e@gmail.com").
    /// </summary>
    public static string MaskEmail(this string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            return "***";
        }

        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];

        var maskedLocal = local.Length switch
        {
            <= 1 => "***",
            2 => $"{local[0]}***",
            _ => $"{local[0]}***{local[^1]}",
        };

        return $"{maskedLocal}@{domain}";
    }
}
