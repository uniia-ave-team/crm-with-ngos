namespace Crm.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Translates provider-specific database exceptions into provider-agnostic domain exceptions.
/// </summary>
public interface IDbExceptionTranslator
{
    /// <summary>
    /// Attempts to translate a database or ORM exception into a domain exception.
    /// </summary>
    /// <param name="exception">The exception raised during a database operation.</param>
    /// <returns>A translated domain exception if the failure maps to a known domain condition; otherwise, <c>null</c>.</returns>
    Exception? Translate(Exception exception);
}
