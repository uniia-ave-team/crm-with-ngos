namespace Crm.Domain.Enums;

/// <summary>
/// Specifies the grammatical category or preferred pronoun type for a user,
/// used for role declension, feminitives, and system logic.
/// </summary>
public enum PronounCategory : byte
{
    /// <summary>
    /// No grammatical pronoun category specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Masculine pronouns or forms (e.g., "he/him").
    /// </summary>
    Masculine = 1,

    /// <summary>
    /// Feminine pronouns or forms (e.g., "she/her").
    /// </summary>
    Feminine = 2,

    /// <summary>
    /// Non-binary pronouns or forms (e.g., "they/them").
    /// </summary>
    Neutral = 3,
}
