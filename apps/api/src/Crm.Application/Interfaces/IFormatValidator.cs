using Crm.Application.Dtos.Common;

namespace Crm.Application.Interfaces;

/// <summary>
/// Defines a contract for validating and sanitizing files of specific formats.
/// </summary>
public interface IFormatValidator
{
    /// <summary>
    /// Determines whether the current validator can handle the specified file extension.
    /// </summary>
    /// <param name="extension">
    /// The file extension to check (e.g., <c>".png"</c>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the validator supports the given extension; otherwise, <see langword="false"/>.
    /// </returns>
    bool CanHandle(string extension);

    /// <summary>
    /// Validates the provided file stream against format structure and security rules.
    /// </summary>
    /// <param name="stream">The file content stream to validate.</param>
    /// <param name="extension">The file extension used to determine validation rules.</param>
    /// <returns>
    /// A <see cref="FileValidationResult"/> containing the validation status
    /// and an optional sanitized stream if the content was modified during validation.
    /// </returns>
    FileValidationResult Validate(Stream stream, string extension);
}
