namespace RoEFactura;

/// <summary>
/// Names of the named <see cref="System.Net.Http.HttpClient"/> registrations that RoEFactura creates through
/// <see cref="System.Net.Http.IHttpClientFactory"/>, so a consumer can configure them (handlers, resilience, timeout).
/// </summary>
public static class RoEFacturaHttpClients
{
    /// <summary>
    /// Name of the client used for every ANAF OAuth token request: the authorization-code exchange and the refresh.
    /// The certificate-based flow does not use it. It is registered by all <c>AddRoEFactura*</c> overloads.
    /// The value is part of the package contract and never changes.
    /// <para>
    /// Consumers must configure this client without automatic retries for POST. ANAF invalidates the previous
    /// refresh token on every refresh and an authorization code is single-use, so a retried request re-sends a
    /// credential that has already been consumed and the authorization is lost. The library itself never retries.
    /// </para>
    /// </summary>
    public const string AnafOAuth = "RoEFactura.AnafOAuth";
}
