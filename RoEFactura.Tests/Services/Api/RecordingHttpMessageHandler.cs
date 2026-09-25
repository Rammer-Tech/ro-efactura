using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace RoEFactura.Tests.Services.Api;

/// <summary>
/// Records outgoing requests (method, URI, Authorization, content media type and body bytes) and
/// returns a configurable response. Never performs a real network call.
/// </summary>
public sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    public RecordingHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public List<RecordedRequest> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        byte[]? body = null;
        string? mediaType = null;

        if (request.Content != null)
        {
            body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            mediaType = request.Content.Headers.ContentType?.MediaType;
        }

        Requests.Add(new RecordedRequest(
            request.Method,
            request.RequestUri!,
            request.Headers.Authorization,
            mediaType,
            body));

        return await _responder(request, cancellationToken);
    }

    /// <summary>Returns a fixed text response (e.g. XML or JSON) with the given status and media type.</summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Returning(
        HttpStatusCode status,
        string body,
        string mediaType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return (_, _) =>
        {
            HttpResponseMessage response = new(status)
            {
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body))
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            ApplyHeaders(response, headers);
            return Task.FromResult(response);
        };
    }

    /// <summary>Returns a fixed binary response (e.g. a ZIP or a PDF).</summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> ReturningBytes(
        HttpStatusCode status,
        byte[] body,
        string? mediaType = null,
        string? contentDispositionFileName = null,
        IReadOnlyDictionary<string, string>? headers = null)
    {
        return (_, _) =>
        {
            HttpResponseMessage response = new(status)
            {
                Content = new ByteArrayContent(body)
            };

            if (mediaType != null)
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

            if (contentDispositionFileName != null)
            {
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileNameStar = contentDispositionFileName
                };
            }

            ApplyHeaders(response, headers);
            return Task.FromResult(response);
        };
    }

    /// <summary>Never completes on its own; awaits <paramref name="ct"/> cancellation and throws.</summary>
    public static Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Hanging()
    {
        return async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable: Task.Delay(Infinite) only returns via cancellation.");
        };
    }

    private static void ApplyHeaders(HttpResponseMessage response, IReadOnlyDictionary<string, string>? headers)
    {
        if (headers == null)
            return;

        foreach (KeyValuePair<string, string> header in headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }
}

public sealed record RecordedRequest(
    HttpMethod Method,
    Uri Uri,
    AuthenticationHeaderValue? Authorization,
    string? ContentMediaType,
    byte[]? Body);
