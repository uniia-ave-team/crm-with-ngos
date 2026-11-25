namespace Crm.Domain.Enums;

/// <summary>
/// Categorizes short-lived opaque user action tokens stored in the database.
/// </summary>
public enum ActionTokenType : byte
{
    /// <summary>
    /// A one-time invitation token used to onboard and register a new user.
    /// </summary>
    Invitation = 0,

    /// <summary>
    /// A one-time password reset token used to set a new password for an existing user.
    /// </summary>
    PasswordReset = 1,
}
