using Crm.Application.Dtos.Common;
using FluentValidation;

namespace Crm.Application.Features.Users.Validators;

/// <summary>
/// Provides shared FluentValidation password complexity rule extensions.
/// </summary>
public static class PasswordValidationRuleExtensions
{
    /// <summary>
    /// The maximum allowed length for a user password.
    /// </summary>
    public const int MaxPasswordLength = 128;

    /// <summary>
    /// Applies standard password presence, length, and configured complexity rules to the string rule builder.
    /// </summary>
    /// <typeparam name="T">The type of the command or model being validated.</typeparam>
    /// <param name="ruleBuilder">The FluentValidation rule builder for the password property.</param>
    /// <param name="options">The configured identity password complexity options.</param>
    /// <returns>The configured rule builder options for further chaining.</returns>
    public static IRuleBuilderOptions<T, string> ApplyPasswordRules<T>(
        this IRuleBuilder<T, string> ruleBuilder,
        IdentityOptionsDto options)
    {
        var builder = ruleBuilder
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(options.RequiredLength)
                .WithMessage($"Password must be at least {options.RequiredLength} characters long.")
            .MaximumLength(MaxPasswordLength)
                .WithMessage($"Password must not exceed {MaxPasswordLength} characters.");

        if (options.RequireDigit)
        {
            builder = builder
                .Matches("[0-9]").WithMessage("Password must contain at least one digit.");
        }

        if (options.RequireUppercase)
        {
            builder = builder
                .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter.");
        }

        if (options.RequireLowercase)
        {
            builder = builder
                .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter.");
        }

        if (options.RequireNonAlphanumeric)
        {
            builder = builder
                .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one non-alphanumeric character.");
        }

        return builder;
    }
}
