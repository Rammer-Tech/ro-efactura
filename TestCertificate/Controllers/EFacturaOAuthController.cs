using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TestCertificate.Models;
using TestCertificate.Services;

namespace TestCertificate.Controllers;

[ApiController]
[Route("api/integrations/einvoice")]
public class EFacturaOAuthController : ControllerBase
{
    private readonly ITokenStore _tokenStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EFacturaOAuthController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    string clientId = "2304bebb151ee370a47a34f5ffb92edd0c58d20f9b15ae68";
    string clientSecret = "c1ff456c384a9eb97918b9b69acf53098b5090627ea12edd0c58d20f9b15ae68";
    string redirectUri = "https://localhost:7205/api/integrations/einvoice/callback";
    string tokenUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/token";
    string authorizeUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize";


    public EFacturaOAuthController(
        ITokenStore tokenStore,
        IConfiguration configuration,
        ILogger<EFacturaOAuthController> logger,
        IHttpClientFactory httpClientFactory)
    {
        _tokenStore = tokenStore;
        _configuration = configuration;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Initiate OAuth flow - exactly like SmartBill
    /// </summary>
    [HttpPost("initiate")]
    public IActionResult InitiateOAuth()
    {
        try
        {
            // Generate state for CSRF protection (like SmartBill's long state)
            var state = GenerateState();
            _tokenStore.SaveState(state);

            // Build authorization URL - exactly like SmartBill
            var authUrl = $"{authorizeUrl}?" +
                $"response_type=code&" +
                $"client_id={Uri.EscapeDataString(clientId!)}&" +
                $"redirect_uri={Uri.EscapeDataString(redirectUri!)}&" +
                $"state={state}&" +
                $"token_content_type=jwt"; // SmartBill includes this

            _logger.LogInformation("Generated OAuth URL for client {ClientId}", clientId);

            return Ok(new EFacturaInitiateResponse
            {
                Success = true,
                AuthUrl = authUrl,
                State = state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate OAuth");
            return Ok(new EFacturaInitiateResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// OAuth callback - handles redirect from ANAF
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> OAuthCallback(string? code, string? state)
    {
        try
        {
            _logger.LogInformation("OAuth callback received with code: {Code}, state: {State}",
                code?.Substring(0, Math.Min(10, code?.Length ?? 0)) + "...",
                state?.Substring(0, Math.Min(10, state?.Length ?? 0)) + "...");

            // Validate state (CSRF protection)
            if (string.IsNullOrEmpty(state) || !_tokenStore.ValidateState(state))
            {
                _logger.LogWarning("Invalid state in OAuth callback");
                return Redirect($"http://localhost:3000/efactura-auth?error=invalid_state");
            }

            if (string.IsNullOrEmpty(code))
            {
                _logger.LogWarning("Missing authorization code in OAuth callback");
                return Redirect($"http://localhost:3000/efactura-auth?error=missing_code");
            }

            // Exchange code for token
            EFacturaTokenResponse? tokenResponse = await ExchangeCodeForToken(code);

            if (tokenResponse == null)
            {
                return Redirect($"http://localhost:3000/efactura-auth?error=token_exchange_failed");
            }

            // Store token (using "default" as key for testing)
            var token = new EFacturaToken
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                TokenType = tokenResponse.TokenType,
                Scope = tokenResponse.Scope,
                DebugInfo = new Dictionary<string, object>
                {
                    ["original_expires_in"] = tokenResponse.ExpiresIn,
                    ["received_at"] = DateTime.UtcNow
                }
            };

            _tokenStore.SaveToken("default", token);

            _logger.LogInformation("Token {AccessToken} successfully stored, expires at {ExpiresAt}",token.AccessToken, token.ExpiresAt);

            // Redirect back to React app with success
            return Redirect("http://localhost:3000/efactura-auth?success=true");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback failed");
            return Redirect($"http://localhost:3000/efactura-auth?error={Uri.EscapeDataString(ex.Message)}");
        }
    }

    private async Task<EFacturaTokenResponse?> ExchangeCodeForToken(string code)
    {
        try
        {
            using HttpClient client = _httpClientFactory.CreateClient();

            var authValue = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);

            // Prepare form-urlencoded body
            var par = string.Format(
                "grant_type=authorization_code&code={0}&redirect_uri={1}&token_content_type=jwt",
                Uri.EscapeDataString(code),
                Uri.EscapeDataString(redirectUri)
            );

            var content = new StringContent(par, Encoding.UTF8, "application/x-www-form-urlencoded");

            HttpResponseMessage response = await client.PostAsync(tokenUrl, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            //// Prepare token exchange request as JSON
            //var tokenRequest = new
            //{
            //    grant_type = "authorization_code",
            //    code = code,
            //    client_id = clientId,
            //    client_secret = clientSecret,
            //    redirect_uri = redirectUri
            //};

            //var json = JsonSerializer.Serialize(tokenRequest);
            //var content = new StringContent(json, Encoding.UTF8, "application/json");

            //_logger.LogInformation("Exchanging code for token at {TokenUrl}", tokenUrl);

            //HttpResponseMessage response = await client.PostAsync(tokenUrl, content);
            //string responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Token exchange response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Token exchange failed: {Response}", responseContent);
                return null;
            }

            // Parse response
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            Dictionary<string, JsonElement>? tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent, options);

            if (tokenResponse == null || !tokenResponse.ContainsKey("access_token"))
            {
                _logger.LogError("Invalid token response format: {Response}", responseContent);
                return null;
            }

            return new EFacturaTokenResponse
            {
                AccessToken = tokenResponse["access_token"].GetString() ?? string.Empty,
                RefreshToken = tokenResponse.ContainsKey("refresh_token") ?
                    tokenResponse["refresh_token"].GetString() : null,
                ExpiresIn = tokenResponse.ContainsKey("expires_in") ?
                    tokenResponse["expires_in"].GetInt32() : 3600,
                TokenType = tokenResponse.ContainsKey("token_type") ?
                    tokenResponse["token_type"].GetString() ?? "Bearer" : "Bearer",
                Scope = tokenResponse.ContainsKey("scope") ?
                    tokenResponse["scope"].GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exchanging code for token");
            return null;
        }
    }

    private string GenerateState()
    {
        // Generate a secure random state like SmartBill's long state
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Convert to hex string (64 characters)
        var hex = BitConverter.ToString(bytes).Replace("-", "").ToLower();

        // Add extra entropy with timestamp
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var combined = $"{hex}{timestamp}";

        // Hash for consistent length
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
        var state = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        // Make it longer like SmartBill (double it)
        return state + BitConverter.ToString(bytes).Replace("-", "").ToLower();
    }
}