using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using RoEFactura.Models;

namespace RoEFactura.Services.Authentication;

internal class AnafOAuthClient : IAnafOAuthClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static X509Certificate2? _cachedCertificate;
    private static readonly Lock CertificateLock = new Lock();

    public AnafOAuthClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
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

    private static async Task<Token> GetJwtTokenAsync(string clientId, string clientSecret, string callbackUrl,
        HttpClient client)
    {
        string url =
            $"https://logincert.anaf.ro/anaf-oauth2/v1/authorize?" +
            $"response_type=code&" +
            $"client_id={clientId}&" +
            $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
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

    /// <summary>
    /// Gets all available Romanian certificates for manual selection
    /// </summary>
    public static List<CertificateInfo> GetAvailableRomanianCertificates()
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

    /// <summary>
    /// Clears the certificate cache (useful for testing or when certificate changes)
    /// </summary>
    public static void ClearCertificateCache()
    {
        lock (CertificateLock)
        {
            _cachedCertificate?.Dispose();
            _cachedCertificate = null;
        }
    }

    /// <summary>
    /// Gets a specific certificate by thumbprint
    /// </summary>
    public static X509Certificate2? GetCertificateByThumbprint(string thumbprint)
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
    
    // ===== OAuth Redirect Flow Implementation (for web apps) =====
    
    /// <summary>
    /// Generates the OAuth authorization URL for redirecting users to ANAF
    /// </summary>
    public string GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null)
    {
        string authorizeUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize";
        
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
        
        string url = $"{options.AuthorizeUrl}?" +
                     $"response_type=code&" +
                     $"client_id={Uri.EscapeDataString(options.ClientId)}&" +
                     $"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}";
        
        if (!string.IsNullOrEmpty(state))
        {
            url += $"&state={Uri.EscapeDataString(state)}";
        }
        
        if (options.IncludeTokenContentType)
        {
            url += "&token_content_type=jwt";
        }
        
        return url;
    }
    
    /// <summary>
    /// Exchanges an authorization code for access token
    /// </summary>
    public async Task<Token> ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri)
    {
        string tokenUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/token";
        
        using HttpClient client = _httpClientFactory.CreateClient();
        
        // Use Basic authentication header (following the working implementation)
        string authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        
        // Prepare form-urlencoded body
        string formData = $"grant_type=authorization_code&" +
                          $"code={Uri.EscapeDataString(code)}&" +
                          $"redirect_uri={Uri.EscapeDataString(redirectUri)}&" +
                          $"token_content_type=jwt";
        
        StringContent content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
        
        HttpResponseMessage response = await client.PostAsync(tokenUrl, content);
        string responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Token exchange failed. HTTP Status: {response.StatusCode}. " +
                $"Response: {responseContent}");
        }
        
        // Parse the response
        JsonSerializerOptions options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        Dictionary<string, JsonElement>? tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent, options);
        
        if (tokenResponse == null || !tokenResponse.ContainsKey("access_token"))
        {
            throw new InvalidOperationException($"Invalid token response format: {responseContent}");
        }
        
        return new Token
        {
            AccessToken = tokenResponse["access_token"].GetString() ?? string.Empty,
            RefreshToken = tokenResponse.ContainsKey("refresh_token") ? 
                tokenResponse["refresh_token"].GetString() ?? string.Empty : string.Empty,
            ExpiresIn = tokenResponse.ContainsKey("expires_in") ? 
                tokenResponse["expires_in"].GetInt32() : 3600,
            TokenType = tokenResponse.ContainsKey("token_type") ? 
                tokenResponse["token_type"].GetString() ?? "Bearer" : "Bearer",
            Scope = tokenResponse.ContainsKey("scope") ? 
                tokenResponse["scope"].GetString() ?? string.Empty : string.Empty
        };
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
        
        using HttpClient client = _httpClientFactory.CreateClient();
        
        // Use Basic authentication header
        string authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{options.ClientId}:{options.ClientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
        
        // Prepare form-urlencoded body
        string formData = $"grant_type=authorization_code&" +
                          $"code={Uri.EscapeDataString(code)}&" +
                          $"redirect_uri={Uri.EscapeDataString(options.RedirectUri)}";
        
        if (options.IncludeTokenContentType)
        {
            formData += "&token_content_type=jwt";
        }
        
        StringContent content = new StringContent(formData, Encoding.UTF8, "application/x-www-form-urlencoded");
        
        HttpResponseMessage response = await client.PostAsync(options.TokenUrl, content);
        string responseContent = await response.Content.ReadAsStringAsync();
        
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Token exchange failed. HTTP Status: {response.StatusCode}. " +
                $"Response: {responseContent}");
        }
        
        // Parse the response
        JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        Dictionary<string, JsonElement>? tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent, jsonOptions);
        
        if (tokenResponse == null || !tokenResponse.ContainsKey("access_token"))
        {
            throw new InvalidOperationException($"Invalid token response format: {responseContent}");
        }
        
        return new Token
        {
            AccessToken = tokenResponse["access_token"].GetString() ?? string.Empty,
            RefreshToken = tokenResponse.ContainsKey("refresh_token") ? 
                tokenResponse["refresh_token"].GetString() ?? string.Empty : string.Empty,
            ExpiresIn = tokenResponse.ContainsKey("expires_in") ? 
                tokenResponse["expires_in"].GetInt32() : 3600,
            TokenType = tokenResponse.ContainsKey("token_type") ? 
                tokenResponse["token_type"].GetString() ?? "Bearer" : "Bearer",
            Scope = tokenResponse.ContainsKey("scope") ? 
                tokenResponse["scope"].GetString() ?? string.Empty : string.Empty
        };
    }
}
