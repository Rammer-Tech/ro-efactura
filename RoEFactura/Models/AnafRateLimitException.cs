using System.Net;

namespace RoEFactura.Models;

/// <summary>
/// ANAF returned HTTP 429 (rate limit exceeded). See <see cref="RetryAfter"/> for the delay ANAF
/// suggested, when present.
/// </summary>
public sealed class AnafRateLimitException : AnafApiException
{
    public AnafRateLimitException(TimeSpan? retryAfter, string? rawResponse)
        : base(HttpStatusCode.TooManyRequests, "ANAF rate limit exceeded (HTTP 429).", rawResponse)
    {
        RetryAfter = retryAfter;
    }

    /// <summary>The delay ANAF suggested before retrying, parsed from the <c>Retry-After</c> header.</summary>
    public TimeSpan? RetryAfter { get; }
}
