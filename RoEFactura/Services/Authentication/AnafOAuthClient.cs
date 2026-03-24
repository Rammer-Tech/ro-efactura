using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
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
        const string authorizeUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize";
        
        string url = $"{authorizeUrl}?" +
                     $"response_type=code&" +
                     $"client_id={Uri.EscapeDataString(clientId)}&" +
                     $"redirect_uri={Uri.EscapeDataString(redirectUri)}";
        
        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }
        
        // Include token_content_type=jwt following SmartBill pattern
        url += "&token_content_type=jwt";
        
        return url;
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
        
        var url = GenerateAuthorizationUrl(options.ClientId, options.RedirectUri, state);
        
        return url;
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

    const string tokenUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/token";
    
    HttpClient? client = null;
    try
    {
        client = _httpClientFactory.CreateClient();
        
        // Configure timeout if not already set by the factory
        if (client.Timeout == TimeSpan.FromSeconds(100)) // Default timeout
        {
            client.Timeout = TimeSpan.FromSeconds(30); // More reasonable timeout
        }
        
        // Use Basic authentication header
        string authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        
        // Prepare form-urlencoded body
        string formData = $"grant_type=authorization_code&" +
                          $"code={Uri.EscapeDataString(code)}&" +
                          $"client_id={Uri.EscapeDataString(clientId)}&" +
                          $"client_secret={Uri.EscapeDataString(clientSecret)}&" +
                          $"redirect_uri={redirectUri}&" +
                          $"token_content_type=jwt";
        
        using var content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
        
        HttpResponseMessage? response = null;
        string? responseContent = null;
        
        try
        {
            response = await client.PostAsync(tokenUrl, content);
            responseContent = await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException httpEx)
        {
            // Network-related errors (DNS, connection refused, etc.)
            _logger?.LogError(httpEx, "Network error during token exchange");
            throw new TokenExchangeException(
                "Failed to connect to the OAuth server. Please check your network connection.",
                TokenExchangeErrorType.NetworkError,
                httpEx);
        }
        catch (TaskCanceledException tcEx)
        {
            // Timeout occurred
            _logger?.LogError(tcEx, "Request timeout during token exchange");
            throw new TokenExchangeException(
                $"The request to the OAuth server timed out after {client.Timeout.TotalSeconds} seconds.",
                TokenExchangeErrorType.Timeout,
                tcEx);
        }
        
        // Handle non-success status codes with detailed error info
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogError("Token exchange failed with status {StatusCode}: {Response}", 
                response.StatusCode, responseContent);
            
            // Try to parse error response if it's JSON
            string? errorDescription = null;
            string? errorCode = null;
            
            try
            {
                var errorResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                    responseContent ?? string.Empty,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                if (errorResponse != null)
                {
                    errorCode = errorResponse.ContainsKey("error") ? 
                        errorResponse["error"].GetString() : null;
                    errorDescription = errorResponse.ContainsKey("error_description") ? 
                        errorResponse["error_description"].GetString() : null;
                }
            }
            catch
            {
                // If we can't parse the error response, we'll just use the raw content
            }
            
            TokenExchangeErrorType errorType = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => TokenExchangeErrorType.AuthenticationFailed,
                HttpStatusCode.BadRequest => TokenExchangeErrorType.InvalidRequest,
                HttpStatusCode.TooManyRequests => TokenExchangeErrorType.RateLimited,
                HttpStatusCode.ServiceUnavailable => TokenExchangeErrorType.ServiceUnavailable,
                HttpStatusCode.InternalServerError => TokenExchangeErrorType.ServerError,
                _ => TokenExchangeErrorType.UnknownError
            };
            
            throw new TokenExchangeException(
                $"Token exchange failed: {errorCode ?? response.StatusCode.ToString()}. " +
                $"{errorDescription ?? responseContent}",
                errorType,
                statusCode: response.StatusCode,
                serverResponse: responseContent);
        }
        
        // Parse the successful response
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                responseContent ?? string.Empty, options);
            
            if (tokenResponse == null)
            {
                throw new TokenExchangeException(
                    "Received null or empty response from OAuth server",
                    TokenExchangeErrorType.InvalidResponse,
                    serverResponse: responseContent);
            }
            
            // Validate required fields
            if (!tokenResponse.ContainsKey("access_token"))
            {
                _logger?.LogError("Token response missing access_token: {Response}", responseContent);
                throw new TokenExchangeException(
                    "Invalid token response: missing access_token field",
                    TokenExchangeErrorType.InvalidResponse,
                    serverResponse: responseContent);
            }
            
            string? accessToken = tokenResponse["access_token"].GetString();
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new TokenExchangeException(
                    "Received empty access token from OAuth server",
                    TokenExchangeErrorType.InvalidResponse,
                    serverResponse: responseContent);
            }
            
            // Extract optional fields with safe defaults
            var refreshToken = tokenResponse.TryGetValue("refresh_token", out JsonElement value) ? value.GetString() : null;
            
            var expiresIn = 3600; // Default to 1 hour
            if (tokenResponse.ContainsKey("expires_in"))
            {
                try
                {
                    expiresIn = tokenResponse["expires_in"].GetInt32();
                }
                catch (InvalidOperationException)
                {
                    _logger?.LogWarning("Could not parse expires_in value, using default of 3600 seconds");
                }
            }
            
            string tokenType = tokenResponse.ContainsKey("token_type") ? 
                tokenResponse["token_type"].GetString() ?? "Bearer" : "Bearer";
            
            string? scope = tokenResponse.ContainsKey("scope") ? 
                tokenResponse["scope"].GetString() : null;
            
            _logger?.LogInformation("Successfully exchanged authorization code for access token");
            
            return new Token
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken ?? string.Empty,
                ExpiresIn = expiresIn,
                TokenType = tokenType,
                Scope = scope ?? string.Empty,
            };
        }
        catch (JsonException jsonEx)
        {
            _logger?.LogError(jsonEx, "Failed to parse token response JSON: {Response}", responseContent);
            throw new TokenExchangeException(
                "Failed to parse token response from OAuth server",
                TokenExchangeErrorType.InvalidResponse,
                jsonEx,
                serverResponse: responseContent);
        }
        catch (InvalidOperationException ioEx) when (ioEx.Message.Contains("JsonElement"))
        {
            _logger?.LogError(ioEx, "Failed to extract values from token response: {Response}", responseContent);
            throw new TokenExchangeException(
                "Token response contained unexpected data types",
                TokenExchangeErrorType.InvalidResponse,
                ioEx,
                serverResponse: responseContent);
        }
    }
    catch (TokenExchangeException)
    {
        // Re-throw our custom exceptions as-is
        throw;
    }
    catch (ArgumentNullException)
    {
        // Re-throw argument validation exceptions
        throw;
    }
    catch (Exception ex)
    {
        // Catch any unexpected exceptions
        _logger?.LogError(ex, "Unexpected error during token exchange");
        throw new TokenExchangeException(
            "An unexpected error occurred during token exchange",
            TokenExchangeErrorType.UnknownError,
            ex);
    }
}

    /// <summary>
    /// Exchanges an authorization code for access token using configured options
    /// </summary>
    public async Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options)
    {
        if (options == null || !options.IsValid())
        {
            throw new ArgumentException("Invalid OAuth options provided");
        }
        
        Token token = await ExchangeAuthorizationCodeAsync(code, options.ClientId, options.ClientSecret, options.RedirectUri);

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
        string url =
            $"https://logincert.anaf.ro/anaf-oauth2/v1/authorize?" +
            $"client_id={clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
            $"response_type=code&" +
            $"token_content_type=jwt";

        HttpResponseMessage response = await client.GetAsync(url);

        if (response.RequestMessage?.RequestUri?.Query == null ||
            !response.RequestMessage.RequestUri.Query.Contains("code="))
        {
            string actualUri = response.RequestMessage?.RequestUri?.ToString() ?? "Unknown";
            HttpStatusCode statusCode = response.StatusCode;
            throw new InvalidOperationException(
                $"ANAF OAuth authorization failed. Expected authorization code in callback URL but received: {actualUri}. " +
                $"HTTP Status: {statusCode}. This may indicate certificate authentication failure or invalid OAuth parameters.");
        }

        string code = response.RequestMessage.RequestUri.Query.Replace("?code=", "");
        string postData =
            $"grant_type=authorization_code&" +
            $"code={code}&" +
            $"client_id={clientId}&" +
            $"client_secret={clientSecret}&" +
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
            $"token_content_type=jwt";

        response = await client.PostAsync("https://logincert.anaf.ro/anaf-oauth2/v1/token",
            new StringContent(postData));
        string resultContent = await response.Content.ReadAsStringAsync();

        if (!resultContent.Contains("access_token"))
        {
            HttpStatusCode statusCode = response.StatusCode;
            throw new InvalidOperationException(
                $"ANAF OAuth token exchange failed. HTTP Status: {statusCode}. " +
                $"Response: {resultContent}. " +
                $"This may indicate invalid client credentials, expired authorization code, or ANAF service issues.");
        }

        return JsonSerializer.Deserialize<Token>(resultContent);
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
