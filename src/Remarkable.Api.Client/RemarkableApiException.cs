namespace Remarkable.Api.Client;

/// <summary>
/// Thrown when the reMarkable cloud returns a non-success status. Inspect <see cref="StatusCode"/>
/// for the HTTP status returned by the server, and <see cref="Exception.Message"/> for the
/// response body.
/// </summary>
public sealed class RemarkableApiException : Exception
{
    public int StatusCode { get; }

    public RemarkableApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}
