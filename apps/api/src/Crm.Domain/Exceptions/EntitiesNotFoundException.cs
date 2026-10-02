namespace Crm.Domain.Exceptions;

/// <summary>
/// Represents an exception that is thrown when one or more entities are not found in the data store.
/// </summary>
public sealed class EntitiesNotFoundException(
    string entityType,
    IReadOnlyCollection<object> missingKeys)
    : Exception(FormatMessage(entityType, missingKeys))
{
    private static string FormatMessage(string entityType, IReadOnlyCollection<object> missingKeys)
    {
        if (missingKeys.Count == 0)
        {
            return $"One or more entities of type '{entityType}' were not found.";
        }

        var keysString = string.Join(", ", missingKeys.Select(k => $"'{k}'"));
        return $"Entities of type '{entityType}' with keys [{keysString}] were not found.";
    }
}
