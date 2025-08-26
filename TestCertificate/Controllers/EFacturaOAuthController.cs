using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TestCertificate.Models;
using TestCertificate.Services;

namespace TestCertificate.Controllers;

[ApiController]
[Route("api/efactura/oauth")]
public class EFacturaOAuthController : ControllerBase
{
    private readonly ITokenStore _tokenStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EFacturaOAuthController> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    
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
            
            // Get OAuth configuration
            var clientId = _configuration["AnafOAuth:ClientId"];
            var redirectUri = _configuration["AnafOAuth:RedirectUri"];
            var authorizeUrl = _configuration["AnafOAuth:AuthorizeUrl"];
            
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
            var tokenResponse = await ExchangeCodeForToken(code);
            
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
            
            _logger.LogInformation("Token successfully stored, expires at {ExpiresAt}", token.ExpiresAt);
            
            // Redirect back to React app with success
            return Redirect("http://localhost:3000/efactura-auth?success=true");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback failed");
            return Redirect($"http://localhost:3000/efactura-auth?error={Uri.EscapeDataString(ex.Message)}");
        }
    }
    
    /// <summary>
    /// Get current authorization status
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetAuthStatus()
    {
        var token = _tokenStore.GetToken("default");
        
        if (token == null)
        {
            return Ok(new EFacturaAuthStatus
            {
                IsAuthorized = false
            });
        }
        
        return Ok(new EFacturaAuthStatus
        {
            IsAuthorized = true,
            ExpiresAt = token.ExpiresAt,
            TokenType = token.TokenType,
            TokenInfo = new Dictionary<string, object>
            {
                ["has_refresh_token"] = !string.IsNullOrEmpty(token.RefreshToken),
                ["created_at"] = token.CreatedAt,
                ["scope"] = token.Scope ?? "N/A",
                ["debug_info"] = token.DebugInfo ?? new Dictionary<string, object>()
            }
        });
    }
    
    /// <summary>
    /// Clear stored tokens
    /// </summary>
    [HttpDelete("clear")]
    public IActionResult ClearAuthorization()
    {
        _tokenStore.ClearToken("default");
        _logger.LogInformation("Authorization cleared");
        
        return Ok(new { success = true, message = "Authorization cleared" });
    }
    
    /// <summary>
    /// Manual code exchange for testing
    /// </summary>
    [HttpPost("exchange-code")]
    public async Task<IActionResult> ExchangeCode([FromBody] EFacturaCodeExchangeRequest request)
    {
        try
        {
            var tokenResponse = await ExchangeCodeForToken(request.Code);
            
            if (tokenResponse == null)
            {
                return BadRequest(new { success = false, error = "Token exchange failed" });
            }
            
            // Store token
            var token = new EFacturaToken
            {
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                TokenType = tokenResponse.TokenType,
                Scope = tokenResponse.Scope
            };
            
            _tokenStore.SaveToken("default", token);
            
            return Ok(new { success = true, data = tokenResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual code exchange failed");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
    
    private async Task<EFacturaTokenResponse?> ExchangeCodeForToken(string code)
    {
        try
        {
            var clientId = _configuration["AnafOAuth:ClientId"];
            var clientSecret = _configuration["AnafOAuth:ClientSecret"];
            var redirectUri = _configuration["AnafOAuth:RedirectUri"];
            var tokenUrl = _configuration["AnafOAuth:TokenUrl"];
            
            using var client = _httpClientFactory.CreateClient();
            
            // Prepare token exchange request - exactly like SmartBill
            var formData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("client_id", clientId!),
                new KeyValuePair<string, string>("client_secret", clientSecret!),
                new KeyValuePair<string, string>("redirect_uri", redirectUri!)
            });
            
            _logger.LogInformation("Exchanging code for token at {TokenUrl}", tokenUrl);
            
            var response = await client.PostAsync(tokenUrl, formData);
            var responseContent = await response.Content.ReadAsStringAsync();
            
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
            
            var tokenResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(responseContent, options);
            
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