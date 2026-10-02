using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Extensions;

/// <summary>
/// Provides extension methods for <see cref="IdentityResult"/> to simplify error handling and formatting.
/// </summary>
public static class IdentityResultExtensions
{
    private const string ErrorSeparator = " | ";

    /// <summary>
    /// Extracts and concatenates error descriptions from an identity result into a single formatted string
    /// separated by a pipeline (" | ").
    /// </summary>
    /// <param name="result">The identity result containing the errors.</param>
    /// <returns>A formatted string with all error descriptions, or an empty string if there are no errors.</returns>
    public static string FormatErrors(this IdentityResult result) =>
        string.Join(ErrorSeparator, result.Errors.Select(e => e.Description));
}
