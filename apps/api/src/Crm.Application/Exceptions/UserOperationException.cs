using Crm.Application.Enums;

namespace Crm.Application.Exceptions;

/// <summary>
/// Represents an exception that is thrown when a user management operation fails.
/// </summary>
public sealed class UserOperationException(UserOperation operation, string errors)
    : Exception(FormatMessage(operation, errors))
{
    private static string FormatMessage(UserOperation operation, string errors) =>
        $"User operation '{operation}' failed: {errors}";
}
