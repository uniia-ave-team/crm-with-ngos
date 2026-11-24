using System.Security.Cryptography;
using Crm.Application.Dtos.Common;

namespace Crm.Cli.Services;

/// <summary>
/// Generates cryptographically secure random passwords that satisfy the configured <see cref="IdentityOptionsDto"/> policy.
/// </summary>
public static class CompliantPasswordGenerator
{
    private const int MinimumGeneratedLength = 16;
    private const string LowercaseChars = "abcdefghijklmnopqrstuvwxyz";
    private const string UppercaseChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const string DigitChars = "0123456789";
    private const string NonAlphanumericChars = "!@#$%^&*()-_=+[]?";
    private const string AllChars = LowercaseChars + UppercaseChars + DigitChars + NonAlphanumericChars;

    /// <summary>
    /// Generates a random password meeting all password complexity requirements specified in <paramref name="options"/>.
    /// </summary>
    /// <param name="options">The active identity password policy options.</param>
    /// <returns>A generated password string.</returns>
    public static string Generate(IdentityOptionsDto options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var requiredChars = new List<char>(4);

        if (options.RequireLowercase)
        {
            requiredChars.Add(PickRandomChar(LowercaseChars));
        }

        if (options.RequireUppercase)
        {
            requiredChars.Add(PickRandomChar(UppercaseChars));
        }

        if (options.RequireDigit)
        {
            requiredChars.Add(PickRandomChar(DigitChars));
        }

        if (options.RequireNonAlphanumeric)
        {
            requiredChars.Add(PickRandomChar(NonAlphanumericChars));
        }

        int targetLength = Math.Max(Math.Max(options.RequiredLength, MinimumGeneratedLength), requiredChars.Count);
        char[] password = new char[targetLength];

        for (int i = 0; i < requiredChars.Count; i++)
        {
            password[i] = requiredChars[i];
        }

        for (int i = requiredChars.Count; i < targetLength; i++)
        {
            password[i] = PickRandomChar(AllChars);
        }

        RandomNumberGenerator.Shuffle(password.AsSpan());

        return new string(password);
    }

    private static char PickRandomChar(string alphabet)
        => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}
