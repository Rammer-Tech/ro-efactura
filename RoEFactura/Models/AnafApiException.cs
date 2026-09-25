using System.Net;

namespace RoEFactura.Models;

/// <summary>
/// A non-2xx response from an ANAF e-Factura endpoint. Subclasses <see cref="HttpRequestException"/> so
/// existing <c>catch (HttpRequestException)</c> callers keep working, while callers that need ANAF's
/// error details can catch this type (or <see cref="AnafRateLimitException"/>) specifically.
/// Transport failures (DNS, connection refused, etc.) still surface as a plain
/// <see cref="HttpRequestException"/>, so the two can be told apart.
/// </summary>
public class AnafApiException : HttpRequestException
{
    public AnafApiException(
        HttpStatusCode? statusCode,
        string message,
        string? rawResponse,
        IReadOnlyList<string>? errors = null,
        Exception? innerException = null)
        : base(message, innerException, statusCode)
    {
        RawResponse = rawResponse;
        Errors = errors ?? [];
    }

    /// <summary>The raw response body ANAF returned, if any.</summary>
    public string? RawResponse { get; }

    /// <summary>Error messages ANAF returned alongside the failure, if any.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>True when the status code is 401 Unauthorized or 403 Forbidden.</summary>
    public bool IsUnauthorized => StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
