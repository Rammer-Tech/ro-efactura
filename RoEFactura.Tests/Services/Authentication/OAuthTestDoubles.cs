using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace RoEFactura.Tests.Services.Authentication;

/// <summary>
/// A configurable <see cref="HttpMessageHandler"/> that records the outgoing request (method, URI, Authorization
/// header, content media type and body) and returns a pre-configured response. Owned by WP-B: not shared with the
/// WP-A test doubles under <c>RoEFactura.Tests.Services.Api</c>.
/// </summary>
internal sealed class OAuthRecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

    private OAuthRecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        _responder = responder;
    }

    public string? LastRequestUri { get; private set; }

    public string? LastAuthorizationScheme { get; private set; }

    public string? LastAuthorizationParameter { get; private set; }

    public string? LastContentMediaType { get; private set; }

    public string? LastRequestBody { get; private set; }

    public int RequestCount { get; private set; }

    public static OAuthRecordingHttpMessageHandler Returning(
        HttpStatusCode statusCode,
        string body,
        string mediaType = "application/json")
    {
        return new OAuthRecordingHttpMessageHandler((_, _) =>
        {
            HttpResponseMessage response = new(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType),
            };
            return Task.FromResult(response);
        });
    }

    public static OAuthRecordingHttpMessageHandler Hanging()
    {
        return new OAuthRecordingHttpMessageHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            throw new InvalidOperationException("Unreachable: the delay above only completes via cancellation.");
        });
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        LastRequestUri = request.RequestUri?.ToString();
        LastAuthorizationScheme = request.Headers.Authorization?.Scheme;
        LastAuthorizationParameter = request.Headers.Authorization?.Parameter;
        LastContentMediaType = request.Content?.Headers.ContentType?.MediaType;
        LastRequestBody = request.Content != null
            ? await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
            : null;

        return await _responder(request, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// An <see cref="IHttpClientFactory"/> that always returns a client wired to a given handler, with the handler
/// left undisposed (the test owns disposal via the handler's lifetime, per the recording handler being reused
/// across assertions).
/// </summary>
internal sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that captures every formatted log message (and exception, if any) so
/// tests can assert that no token value or response body ever reaches a log entry.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<string> _entries = [];

    public IReadOnlyList<string> Entries => _entries;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        _entries.Add(exception != null ? $"{message} | {exception}" : message);
    }

    private sealed class NoopScope : IDisposable
    {
        public static readonly NoopScope Instance = new();

        public void Dispose()
        {
        }
    }
}
