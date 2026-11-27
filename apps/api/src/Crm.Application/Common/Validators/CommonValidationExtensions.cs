using Crm.Application.Common.Consts;
using Crm.Domain.Constants;
using FluentValidation;

namespace Crm.Application.Common.Validators;

/// <summary>
/// Provides reusable FluentValidation rule builder extensions shared across feature validators
/// (null-character checks, required/optional text length rules, image file name rules, and non-empty stream checks).
/// </summary>
internal static class CommonValidationExtensions
{
    private static readonly string AllowedImageExtensionsMessage =
        $"Invalid file extension. Allowed extensions are: {string.Join(", ", FileStorageConstants.AllowedExtensions)}.";

    /// <summary>
    /// Checks whether <paramref name="value"/> is <see langword="null"/> or free of NUL (<c>'\0'</c>) characters.
    /// </summary>
    /// <param name="value">The candidate string value.</param>
    /// <returns><see langword="true"/> if safe; otherwise, <see langword="false"/>.</returns>
    public static bool ContainsNoNullChar(string? value)
        => value is null || !value.Contains('\0', StringComparison.Ordinal);

    /// <summary>
    /// Forbids NUL (<c>'\0'</c>) characters in a string property, using <see cref="ValidationMessages.NullCharacterNotAllowed"/>.
    /// </summary>
    /// <typeparam name="T">The validated command or query type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder.</param>
    /// <param name="message">Optional custom error message overriding <see cref="ValidationMessages.NullCharacterNotAllowed"/>.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> NotContainNullChars<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        string? message = null)
        => ruleBuilder
            .Must(ContainsNoNullChar)
            .WithMessage(message ?? ValidationMessages.NullCharacterNotAllowed);

    /// <summary>
    /// Applies required, max-length, and no-NUL-character rules to a mandatory text property.
    /// </summary>
    /// <typeparam name="T">The validated command type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder.</param>
    /// <param name="maxLength">The maximum permitted length.</param>
    /// <param name="requiredMessage">The error message when empty.</param>
    /// <param name="maxLengthMessage">The error message when exceeding <paramref name="maxLength"/>.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ApplyRequiredTextRules<T>(
        this IRuleBuilderInitial<T, string> ruleBuilder,
        int maxLength,
        string requiredMessage,
        string maxLengthMessage)
        => ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage(requiredMessage)
            .MaximumLength(maxLength).WithMessage(maxLengthMessage)
            .Must(ContainsNoNullChar).WithMessage(ValidationMessages.NullCharacterNotAllowed);

    /// <summary>
    /// Applies max-length and no-NUL-character rules to an optional text property.
    /// </summary>
    /// <typeparam name="T">The validated command type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder.</param>
    /// <param name="maxLength">The maximum permitted length.</param>
    /// <param name="maxLengthMessage">The error message when exceeding <paramref name="maxLength"/>.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> ApplyOptionalTextRules<T>(
        this IRuleBuilder<T, string?> ruleBuilder,
        int maxLength,
        string maxLengthMessage)
        => ruleBuilder
            .MaximumLength(maxLength).WithMessage(maxLengthMessage)
            .NotContainNullChars();

    /// <summary>
    /// Applies non-whitespace, max-length, and no-NUL-character rules to an optional text property
    /// that must not be blank when supplied.
    /// </summary>
    /// <typeparam name="T">The validated command type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder.</param>
    /// <param name="maxLength">The maximum permitted length.</param>
    /// <param name="blankMessage">The error message when empty or whitespace.</param>
    /// <param name="maxLengthMessage">The error message when exceeding <paramref name="maxLength"/>.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string?> ApplyOptionalNonBlankTextRules<T>(
        this IRuleBuilderInitial<T, string?> ruleBuilder,
        int maxLength,
        string blankMessage,
        string maxLengthMessage)
        => ruleBuilder
            .Cascade(CascadeMode.Stop)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(blankMessage)
            .MaximumLength(maxLength).WithMessage(maxLengthMessage)
            .NotContainNullChars();

    /// <summary>
    /// Applies standard validation rules for an uploaded image file name (required, no NUL characters, allow-listed extension).
    /// </summary>
    /// <typeparam name="T">The validated command type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder for the file name property.</param>
    /// <param name="nullCharMessage">Optional custom message for NUL characters.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, string> ApplyImageFileNameRules<T>(
        this IRuleBuilderInitial<T, string> ruleBuilder,
        string? nullCharMessage = null)
        => ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Original file name is required.")
            .Must(ContainsNoNullChar).WithMessage(nullCharMessage ?? ValidationMessages.NullCharacterNotAllowed)
            .Must(HasAllowedImageExtension).WithMessage(AllowedImageExtensionsMessage);

    /// <summary>
    /// Applies standard validation rules for a readable, seekable, non-empty upload stream.
    /// </summary>
    /// <typeparam name="T">The validated command type.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder for the stream property.</param>
    /// <param name="nullMessage">Optional error message when the stream is <see langword="null"/>.</param>
    /// <param name="emptyMessage">Optional error message when the stream is unreadable, non-seekable, or empty.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, Stream> ApplyNonEmptyStreamRules<T>(
        this IRuleBuilderInitial<T, Stream> ruleBuilder,
        string? nullMessage = null,
        string? emptyMessage = null)
        => ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage(nullMessage ?? "File stream cannot be null.")
            .Must(stream => stream is not null && stream.CanRead && stream.CanSeek && stream.Length > 0)
            .WithMessage(emptyMessage ?? "File stream cannot be empty.");

    private static bool HasAllowedImageExtension(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !ContainsNoNullChar(fileName))
        {
            return false;
        }

        string extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && FileStorageConstants.AllowedExtensions.Contains(extension);
    }
}
