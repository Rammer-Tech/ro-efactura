namespace RoEFactura.Models;

/// <summary>
/// Configuration for the ANAF e-Factura API client. Bound from configuration section
/// <see cref="SectionName"/>. The default environment is <see cref="AnafEnvironment.Test"/>;
/// a missing section, a missing key, or the default value all resolve to Test.
/// </summary>
public sealed class RoEFacturaOptions
{
    public const string SectionName = "RoEFactura";
    public const string TestApiBaseUrl = "https://api.anaf.ro/test/FCTEL/rest";
    public const string ProductionApiBaseUrl = "https://api.anaf.ro/prod/FCTEL/rest";
    public const string DefaultPublicServicesBaseUrl = "https://webservicesp.anaf.ro/prod/FCTEL/rest";

    /// <summary>
    /// The ANAF environment to call for the authenticated (Bearer) endpoints. Defaults to Test.
    /// </summary>
    public AnafEnvironment Environment { get; set; } = AnafEnvironment.Test;

    /// <summary>
    /// Overrides the resolved base URL for the authenticated ANAF endpoints. When set, it must be an
    /// absolute http(s) URL; it takes precedence over <see cref="Environment"/>.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>
    /// Base URL for the public, stateless ANAF services (validate and XML→PDF). These never depend on
    /// the environment: they always target production and never touch SPV.
    /// </summary>
    public string PublicServicesBaseUrl { get; set; } = DefaultPublicServicesBaseUrl;

    /// <summary>
    /// Resolves the base URL for the authenticated (Bearer) ANAF endpoints.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="ApiBaseUrl"/> is set but not an absolute http(s) URL, or <see cref="Environment"/>
    /// holds a value outside the defined enum range.
    /// </exception>
    public string ResolveApiBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            return ValidateAbsoluteUrl(ApiBaseUrl, nameof(ApiBaseUrl));
        }

        return Environment switch
        {
            AnafEnvironment.Test => TestApiBaseUrl,
            AnafEnvironment.Production => ProductionApiBaseUrl,
            _ => throw new InvalidOperationException(
                $"RoEFactura:Environment has an unsupported value: {Environment}.")
        };
    }

    /// <summary>
    /// Resolves the base URL for the public, stateless ANAF services (validate and XML→PDF).
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <see cref="PublicServicesBaseUrl"/> is not an absolute http(s) URL.
    /// </exception>
    public string ResolvePublicServicesBaseUrl()
    {
        return ValidateAbsoluteUrl(PublicServicesBaseUrl, nameof(PublicServicesBaseUrl));
    }

    private static string ValidateAbsoluteUrl(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"RoEFactura:{propertyName} must be an absolute http(s) URL.");
        }

        return value.TrimEnd('/');
    }
}
