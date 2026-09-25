using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using RoEFactura.Models;

namespace RoEFactura.Services.Authentication;

internal class AnafOAuthClient : IAnafOAuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AnafOAuthClient> _logger;
    private static X509Certificate2? _cachedCertificate;
    private static readonly Lock CertificateLock = new Lock();

    public AnafOAuthClient(IHttpClientFactory httpClientFactory, ILogger<AnafOAuthClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<Token> GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl)
    {
        X509Certificate2 certificate = GetCachedCertificate();
        using HttpClient client = CreateClientWithCertificate(certificate);

        return await GetJwtTokenAsync(clientId, clientSecret, callbackUrl, client);
    }

    public async Task<Token> GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret,
        string callbackUrl)
    {
        using HttpClient client = CreateClientWithCertificate(certificate);

        return await GetJwtTokenAsync(clientId, clientSecret, callbackUrl, client);
    }

    /// <summary>
    /// Overload to authenticate with a certificate selected by thumbprint
    /// </summary>
    public async Task<Token> GetAccessTokenAsync(string thumbprint, string clientId, string clientSecret, string callbackUrl)
    {
        X509Certificate2? certificate = GetCertificateByThumbprint(thumbprint);
        if (certificate == null)
        {
            throw new InvalidOperationException($"Certificate with thumbprint '{thumbprint}' not found in certificate store.");
        }

        return await GetAccessTokenAsync(certificate, clientId, clientSecret, callbackUrl);
    }

    /// <summary>
    /// Generates the OAuth authorization URL for redirecting users to ANAF
    /// </summary>
    public string GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null)
    {
        return BuildAuthorizationUrl(
            authorizeUrl: AnafOAuthOptions.DefaultAuthorizeUrl,
            clientId,
            redirectUri,
            state,
            includeTokenContentType: true,
            prompt: null,
            nonce: null);
    }

    /// <summary>
    /// Generates the OAuth authorization URL using configured options
    /// </summary>
    public string GenerateAuthorizationUrl(AnafOAuthOptions options, string? state = null)
    {
        if (options == null || !options.IsValid())
        {
            throw new ArgumentException("Invalid OAuth options provided");
        }

        return BuildAuthorizationUrl(
            options.AuthorizeUrl,
            options.ClientId,
            options.RedirectUri,
            state,
            options.IncludeTokenContentType,
            string.IsNullOrWhiteSpace(options.Prompt) ? null : options.Prompt,
            string.IsNullOrWhiteSpace(options.Nonce) ? null : options.Nonce);
    }

    private static string BuildAuthorizationUrl(
        string authorizeUrl,
        string clientId,
        string redirectUri,
        string? state,
        bool includeTokenContentType,
        string? prompt,
        string? nonce)
    {
        string url = $"{authorizeUrl}?" +
                     $"response_type=code&" +
                     $"client_id={Uri.EscapeDataString(clientId)}&" +
                     $"redirect_uri={Uri.EscapeDataString(redirectUri)}";

        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }

        if (includeTokenContentType)
        {
            url += "&token_content_type=jwt";
        }

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            url += $"&prompt={Uri.EscapeDataString(prompt)}";
        }

        if (!string.IsNullOrWhiteSpace(nonce))
        {
            url += $"&nonce={Uri.EscapeDataString(nonce)}";
        }

        return url;
    }
    
    /// <summary>
    /// Posts a token request (authorization-code exchange or refresh) to <paramref name="tokenUrl"/> with HTTP Basic
    /// client authentication, classifies non-2xx responses into a <see cref="TokenExchangeErrorType"/>, and parses a
    /// successful response into a <see cref="Token"/>. Never logs the request body, the response body or token values.
    /// </summary>
    private async Task<Token> RequestTokenAsync(
        string operation,
        string tokenUrl,
        string clientId,
        string clientSecret,
        IEnumerable<KeyValuePair<string, string>> form,
        CancellationToken ct)
    {
        HttpClient client = _httpClientFactory.CreateClient();

        // Configure timeout if not already set by the factory
        if (client.Timeout == TimeSpan.FromSeconds(100)) // Default timeout
        {
            client.Timeout = TimeSpan.FromSeconds(30); // More reasonable timeout
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl)
        {
            Content = new FormUrlEncodedContent(form),
        };

        string authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authValue);

        HttpResponseMessage response;
        string responseContent;
        try
        {
            response = await client.SendAsync(request, ct).ConfigureAwait(false);
            responseContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch (HttpRequestException httpEx)
        {
            // Network-related errors (DNS, connection refused, etc.)
            _logger.LogWarning(httpEx, "ANAF OAuth {Operation} failed: network error", operation);
            throw new TokenExchangeException(
                "Failed to connect to the OAuth server. Please check your network connection.",
                TokenExchangeErrorType.NetworkError,
                httpEx);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException tcEx)
        {
            // Timeout occurred (not caller-requested cancellation)
            _logger.LogWarning(tcEx, "ANAF OAuth {Operation} failed: timeout", operation);
            throw new TokenExchangeException(
                $"The request to the OAuth server timed out after {client.Timeout.TotalSeconds} seconds.",
                TokenExchangeErrorType.Timeout,
                tcEx);
        }

        // Handle non-success status codes with detailed error info
        if (!response.IsSuccessStatusCode)
        {
            // Try to parse error response if it's JSON; best-effort only
            string? errorCode = null;
            string? errorDescription = null;
            try
            {
                using JsonDocument errorDocument = JsonDocument.Parse(responseContent);
                JsonElement root = errorDocument.RootElement;

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("error", out JsonElement errorElement)
                    && errorElement.ValueKind == JsonValueKind.String)
                {
                    errorCode = errorElement.GetString();
                }

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("error_description", out JsonElement descriptionElement)
                    && descriptionElement.ValueKind == JsonValueKind.String)
                {
                    errorDescription = descriptionElement.GetString();
                }
            }
            catch (JsonException)
            {
                // If we can't parse the error response, we proceed with nulls.
            }
            catch (InvalidOperationException)
            {
                // Best-effort parsing only: an unexpected body shape (e.g. a non-object root, or
                // "error"/"error_description" present but not a string) must not change the exception
                // type the caller sees. The HTTP-status-classified TokenExchangeException still fires
                // below with errorCode/errorDescription left null.
            }

            TokenExchangeErrorType errorType = (response.StatusCode, errorCode) switch
            {
                (HttpStatusCode.BadRequest, "invalid_grant") => TokenExchangeErrorType.InvalidGrant,
                (HttpStatusCode.BadRequest, _) => TokenExchangeErrorType.InvalidRequest,
                (HttpStatusCode.Unauthorized, _) => TokenExchangeErrorType.AuthenticationFailed,
                (HttpStatusCode.TooManyRequests, _) => TokenExchangeErrorType.RateLimited,
                (HttpStatusCode.ServiceUnavailable, _) => TokenExchangeErrorType.ServiceUnavailable,
                (HttpStatusCode.InternalServerError, _) => TokenExchangeErrorType.ServerError,
                _ => TokenExchangeErrorType.UnknownError,
            };

            _logger.LogWarning(
                "ANAF OAuth {Operation} failed: status {StatusCode}, error {ErrorCode}",
                operation,
                (int)response.StatusCode,
                errorCode ?? "(none)");

            throw new TokenExchangeException(
                $"Token {operation} failed: {errorCode ?? response.StatusCode.ToString()}. {errorDescription}",
                errorType,
                statusCode: response.StatusCode,
                serverResponse: responseContent);
        }

        Token token = ParseTokenResponse(responseContent);

        _logger.LogInformation(
            "ANAF OAuth {Operation} succeeded (expires_in={ExpiresIn}s)", operation, token.ExpiresIn);

        return token;
    }

    /// <summary>
    /// Parses a successful ANAF token endpoint response body into a <see cref="Token"/> and stamps
    /// <see cref="Token.IssuedAtUtc"/>. Shared between the OAuth-redirect flow and the certificate flow.
    /// Never includes the response body in a thrown exception: a success body may contain live tokens.
    /// </summary>
    private static Token ParseTokenResponse(string responseContent)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(responseContent);
            JsonElement root = document.RootElement;

            if (!root.TryGetProperty("access_token", out JsonElement accessTokenElement) ||
                accessTokenElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
            {
                throw new TokenExchangeException(
                    "Invalid token response: missing access_token field",
                    TokenExchangeErrorType.InvalidResponse);
            }

            string accessToken = accessTokenElement.GetString()!;

            string? refreshToken = root.TryGetProperty("refresh_token", out JsonElement refreshTokenElement)
                ? refreshTokenElement.GetString()
                : null;

            int expiresIn = 3600; // Default to 1 hour
            if (root.TryGetProperty("expires_in", out JsonElement expiresInElement))
            {
                if (expiresInElement.ValueKind == JsonValueKind.Number && expiresInElement.TryGetInt32(out int numericExpiresIn))
                {
                    expiresIn = numericExpiresIn;
                }
                else if (expiresInElement.ValueKind == JsonValueKind.String &&
                         int.TryParse(expiresInElement.GetString(), out int parsedExpiresIn))
                {
                    expiresIn = parsedExpiresIn;
                }
            }

            string tokenType = root.TryGetProperty("token_type", out JsonElement tokenTypeElement)
                ? tokenTypeElement.GetString() ?? "Bearer"
                : "Bearer";

            string? scope = root.TryGetProperty("scope", out JsonElement scopeElement) ? scopeElement.GetString() : null;

            return new Token
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken ?? string.Empty,
                ExpiresIn = expiresIn,
                TokenType = tokenType,
                Scope = scope ?? string.Empty,
                IssuedAtUtc = DateTimeOffset.UtcNow,
            };
        }
        catch (TokenExchangeException)
        {
            throw;
        }
        catch (JsonException jsonEx)
        {
            throw new TokenExchangeException(
                "Failed to parse token response from OAuth server",
                TokenExchangeErrorType.InvalidResponse,
                jsonEx);
        }
        catch (InvalidOperationException ioEx)
        {
            throw new TokenExchangeException(
                "Token response contained unexpected data types",
                TokenExchangeErrorType.InvalidResponse,
                ioEx);
        }
    }

    /// <summary>
    /// Exchanges an authorization code for access token
    /// </summary>
    public async Task<Token> ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri)
    {
        // Validate input parameters early
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentNullException(nameof(code), "Authorization code cannot be null or empty");
        if (string.IsNullOrWhiteSpace(clientId))
            throw new ArgumentNullException(nameof(clientId), "Client ID cannot be null or empty");
        if (string.IsNullOrWhiteSpace(clientSecret))
            throw new ArgumentNullException(nameof(clientSecret), "Client secret cannot be null or empty");
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ArgumentNullException(nameof(redirectUri), "Redirect URI cannot be null or empty");

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("redirect_uri", redirectUri),
            new("token_content_type", "jwt"),
        };

        return await RequestTokenAsync(
            "exchange", AnafOAuthOptions.DefaultTokenUrl, clientId, clientSecret, form, CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Exchanges an authorization code for access token using configured options
    /// </summary>
    public Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options)
        => ExchangeAuthorizationCodeAsync(code, options, CancellationToken.None);

    /// <summary>
    /// Exchanges an authorization code for access token using configured options, honoring cancellation
    /// </summary>
    public async Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentNullException(nameof(code), "Authorization code cannot be null or empty");
        }

        if (options == null || !options.IsValid())
        {
            throw new ArgumentException("Invalid OAuth options provided");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("client_id", options.ClientId),
            new("client_secret", options.ClientSecret),
            new("redirect_uri", options.RedirectUri),
        };

        if (options.IncludeTokenContentType)
        {
            form.Add(new("token_content_type", "jwt"));
        }

        return await RequestTokenAsync(
            "exchange", options.TokenUrl, options.ClientId, options.ClientSecret, form, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token (and, per ANAF policy, a new refresh token) using configured options
    /// </summary>
    public async Task<Token> RefreshAccessTokenAsync(string refreshToken, AnafOAuthOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

        if (options == null || !options.IsValid())
        {
            throw new ArgumentException("Invalid OAuth options provided");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "refresh_token"),
            new("refresh_token", refreshToken),
        };

        if (options.IncludeTokenContentType)
        {
            form.Add(new("token_content_type", "jwt"));
        }

        Token token = await RequestTokenAsync(
            "refresh", options.TokenUrl, options.ClientId, options.ClientSecret, form, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(token.RefreshToken))
        {
            _logger.LogWarning("ANAF OAuth {Operation} response did not include a new refresh token", "refresh");
        }

        return token;
    }

    /// <summary>
    /// Validates that a certificate is suitable for client authentication with ANAF
    /// </summary>
    private static bool IsValidForClientAuthentication(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate has a private key (required for client authentication)
            if (!certificate.HasPrivateKey)
            {
                return false;
            }

            // Check Enhanced Key Usage extension for Client Authentication
            X509ExtensionCollection extensions = certificate.Extensions;
            foreach (X509Extension extension in extensions)
            {
                if (extension.Oid?.Value == "2.5.29.37") // Enhanced Key Usage OID
                {
                    X509EnhancedKeyUsageExtension? eku = extension as X509EnhancedKeyUsageExtension;
                    if (eku != null)
                    {
                        // Check for Client Authentication OID (1.3.6.1.5.5.7.3.2)
                        foreach (Oid oid in eku.EnhancedKeyUsages)
                        {
                            if (oid.Value == "1.3.6.1.5.5.7.3.2") // Client Authentication
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // If no Enhanced Key Usage extension found, check Key Usage
            foreach (X509Extension extension in extensions)
            {
                if (extension.Oid?.Value == "2.5.29.15") // Key Usage OID
                {
                    X509KeyUsageExtension? ku = extension as X509KeyUsageExtension;
                    if (ku != null)
                    {
                        // Digital Signature and Key Encipherment are required for client auth
                        return ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature) ||
                               ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment);
                    }
                }
            }

            // If no specific usage extensions found, assume it's valid (fallback)
            return true;
        }
        catch
        {
            // If any error occurs during validation, exclude the certificate
            return false;
        }
    }
    
    /// <summary>
    /// Gets all available Romanian certificates for manual selection
    /// </summary>
    private static List<CertificateInfo> GetAvailableRomanianCertificates()
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        try
        {
            X509Certificate2Collection validCertificates =
                store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false);

            List<CertificateInfo> romanianCertificates = new List<CertificateInfo>();

            foreach (X509Certificate2 cert in validCertificates)
            {
                string certificateIssuerName = cert.IssuerName.Name;

                bool isRomanianCA = 
                    certificateIssuerName.Contains("CERTSIGN", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("DIGISIGN", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("TRAN", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("ALFASIGN", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("CERT DIGI", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("CERTDIGITAL", StringComparison.CurrentCultureIgnoreCase) ||
                    certificateIssuerName.Contains("DE CALCUL", StringComparison.CurrentCultureIgnoreCase);

                if (isRomanianCA)
                {
                    romanianCertificates.Add(new CertificateInfo
                    {
                        Certificate = cert,
                        Subject = cert.Subject,
                        Issuer = cert.IssuerName.Name,
                        ExpiryDate = cert.NotAfter,
                        HasPrivateKey = cert.HasPrivateKey,
                        IsValidForClientAuth = IsValidForClientAuthentication(cert),
                        Thumbprint = cert.Thumbprint
                    });
                }
            }

            return romanianCertificates.OrderByDescending(c => c.IsValidForClientAuth)
                                     .ThenBy(c => c.ExpiryDate)
                                     .ToList();
        }
        finally
        {
            store.Close();
        }
    }

    /// <summary>
    /// Gets a specific certificate by thumbprint
    /// </summary>
    private static X509Certificate2? GetCertificateByThumbprint(string thumbprint)
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        try
        {
            X509Certificate2Collection found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
            return found.Count > 0 ? found[0] : null;
        }
        finally
        {
            store.Close();
        }
    }

    private static async Task<Token> GetJwtTokenAsync(string clientId, string clientSecret, string callbackUrl,
        HttpClient client)
    {
        string url = BuildAuthorizationUrl(
            authorizeUrl: AnafOAuthOptions.DefaultAuthorizeUrl,
            clientId,
            callbackUrl,
            state: null,
            includeTokenContentType: true,
            prompt: null,
            nonce: null);

        HttpResponseMessage response = await client.GetAsync(url);

        string? code = ExtractAuthorizationCode(response.RequestMessage?.RequestUri);
        if (string.IsNullOrEmpty(code))
        {
            string actualUri = response.RequestMessage?.RequestUri?.ToString() ?? "Unknown";
            HttpStatusCode statusCode = response.StatusCode;
            throw new InvalidOperationException(
                $"ANAF OAuth authorization failed. Expected authorization code in callback URL but received: {actualUri}. " +
                $"HTTP Status: {statusCode}. This may indicate certificate authentication failure or invalid OAuth parameters.");
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "authorization_code"),
            new("code", code),
            new("client_id", clientId),
            new("client_secret", clientSecret),
            new("redirect_uri", callbackUrl),
            new("token_content_type", "jwt"),
        };

        response = await client.PostAsync(AnafOAuthOptions.DefaultTokenUrl, new FormUrlEncodedContent(form));
        string resultContent = await response.Content.ReadAsStringAsync();

        if (!resultContent.Contains("access_token"))
        {
            HttpStatusCode statusCode = response.StatusCode;
            throw new InvalidOperationException(
                $"ANAF OAuth token exchange failed. HTTP Status: {statusCode}. " +
                $"This may indicate invalid client credentials, expired authorization code, or ANAF service issues.");
        }

        return ParseTokenResponse(resultContent);
    }

    /// <summary>
    /// Extracts the <c>code</c> query-string value from an ANAF authorize-endpoint callback URI, or null when absent.
    /// </summary>
    internal static string? ExtractAuthorizationCode(Uri? uri)
    {
        if (uri == null)
        {
            return null;
        }

        NameValueCollection query = HttpUtility.ParseQueryString(uri.Query);
        return query["code"];
    }

    /// <summary>
    /// Creates HttpClient with certificate configuration for ANAF authentication
    /// Note: Due to certificate requirements, we create a dedicated client with custom handler
    /// </summary>
    private HttpClient CreateClientWithCertificate(X509Certificate2 certificate)
    {
        HttpClientHandler handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            UseProxy = false,
            ClientCertificates = { certificate }
        };

        HttpClient client = new HttpClient(handler);
        
        // Set default timeout and headers similar to IHttpClientFactory defaults
        client.Timeout = TimeSpan.FromSeconds(100);
        client.DefaultRequestHeaders.Add("User-Agent", "RoEFactura/1.0");
        
        return client;
    }

    /// <summary>
    /// Gets certificate with caching to avoid repeated certificate store access
    /// </summary>
    private static X509Certificate2 GetCachedCertificate()
    {
        lock (CertificateLock)
        {
            // Return cached certificate if valid
            if (_cachedCertificate != null && 
                _cachedCertificate.NotAfter > DateTime.Now.AddDays(7)) // Refresh if expires in 7 days
            {
                return _cachedCertificate;
            }

            // Get fresh certificate and cache it
            _cachedCertificate = GetCertificate();
            return _cachedCertificate;
        }
    }

    private static X509Certificate2 GetCertificate(bool getAll = false)
    {
        X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
        store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

        X509Certificate2Collection foundCertificates = store.Certificates;
        X509Certificate2Collection validCertificates =
            foundCertificates.Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false);

        X509Certificate2Collection selectedCertificates = [];

        foreach (X509Certificate2 cert in validCertificates)
        {
            string certificateIssuerName = cert.IssuerName.Name;

            // Check if certificate is from a Romanian CA
            bool isRomanianCA = getAll || 
                certificateIssuerName.Contains("CERTSIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("DIGISIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("TRAN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("ALFASIGN", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("CERT DIGI", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("CERTDIGITAL", StringComparison.CurrentCultureIgnoreCase) ||
                certificateIssuerName.Contains("DE CALCUL", StringComparison.CurrentCultureIgnoreCase);

            // Validate certificate is suitable for client authentication
            if (isRomanianCA && IsValidForClientAuthentication(cert))
            {
                selectedCertificates.Add(cert);
            }
        }

        store.Close(); // Ensure the store is always closed after use

        return selectedCertificates.Count switch
        {
            1 => selectedCertificates[0],
            > 1 => throw new InvalidOperationException(
                $"Multiple valid certificates found ({selectedCertificates.Count}). " +
                $"Certificates: {string.Join(", ", selectedCertificates.Cast<X509Certificate2>().Select(c => $"{c.Subject} (Expires: {c.NotAfter:yyyy-MM-dd})"))}" +
                " Please ensure only one Romanian certificate for client authentication is installed."),
            _ => throw new InvalidOperationException(
                $"No valid Romanian certificates found for client authentication. " +
                $"Found {validCertificates.Count} time-valid certificates total. " +
                $"Please install a valid certificate from: CERTSIGN, DIGISIGN, ALFASIGN, CERTDIGITAL, or other Romanian Certificate Authority.")
        };

        // If no certificates were found or selected, handle this case appropriately:
    }
}
