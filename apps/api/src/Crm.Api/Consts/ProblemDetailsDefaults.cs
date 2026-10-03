namespace Crm.Api.Consts;

public static class ProblemDetailsDefaults
{
    public const string ClientErrorTitle = "Client Error";
    public const string ServerErrorTitle = "Server Error";
    public const string ValidationFailedTitle = "Validation Failed";
    public const string ValidationFailedDetail = "One or more validation errors occurred.";
    public const string UnexpectedErrorDetail = "An unexpected error occurred while processing your request. Please try again later.";
    public const string StackTraceExtensionKey = "stackTrace";
    private const string Rfc9110BaseUrl = "https://tools.ietf.org/html/rfc9110#section-";

    public static string GetTypeUrl(int statusCode)
    {
        return $"{Rfc9110BaseUrl}15-{statusCode}";
    }
}
