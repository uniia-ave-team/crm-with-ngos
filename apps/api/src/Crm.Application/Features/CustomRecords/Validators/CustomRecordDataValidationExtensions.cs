using System.Text.Json.Nodes;
using Crm.Application.Common.Consts;
using Crm.Application.Common.Validators;
using Crm.Domain.Constants.Validation;
using FluentValidation;

namespace Crm.Application.Features.CustomRecords.Validators;

/// <summary>
/// Provides shared FluentValidation rules for validating dynamic <see cref="JsonObject"/> payloads on custom record commands.
/// </summary>
internal static class CustomRecordDataValidationExtensions
{
    /// <summary>
    /// The maximum allowed character length for any individual string value within a custom record JSON payload
    /// to protect the PostgreSQL <c>tsvector</c> 1 MB limit.
    /// </summary>
    public const int MaxStringValueLength = 10_000;

    /// <summary>
    /// Applies shared validation rules to a custom record <see cref="JsonObject"/> data payload.
    /// </summary>
    /// <typeparam name="T">The command type being validated.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder for the <see cref="JsonObject"/> property.</param>
    /// <returns>The configured rule builder options.</returns>
    public static IRuleBuilderOptions<T, JsonObject> ApplyCustomRecordDataRules<T>(
        this IRuleBuilder<T, JsonObject> ruleBuilder)
    {
        return ruleBuilder
            .NotNull().WithMessage("Record data payload is required.")
            .Must(data => data is null || data.Count <= CustomRecordValidationConstants.MaxFields)
            .WithMessage($"Record data payload cannot contain more than {CustomRecordValidationConstants.MaxFields} fields.")
            .Must(NotContainNullCharacters)
            .WithMessage(ValidationMessages.NullCharacterNotAllowed)
            .Must(NotExceedMaxStringValueLength)
            .WithMessage($"Record data string values must not exceed {MaxStringValueLength} characters.");
    }

    private static bool NotContainNullCharacters(JsonObject? data)
    {
        if (data is null)
        {
            return true;
        }

        foreach (var (_, node) in data)
        {
            if (node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var value) &&
                !CommonValidationExtensions.ContainsNoNullChar(value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool NotExceedMaxStringValueLength(JsonObject? data)
    {
        if (data is null)
        {
            return true;
        }

        foreach (var (_, node) in data)
        {
            if (node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var value) &&
                value.Length > MaxStringValueLength)
            {
                return false;
            }
        }

        return true;
    }
}
