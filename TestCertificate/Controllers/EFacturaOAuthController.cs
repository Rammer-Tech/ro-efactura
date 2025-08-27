using Microsoft.AspNetCore.Mvc;
using RoEFactura.Models;
using RoEFactura.Services.Authentication;
using System.Security.Cryptography;
using System.Text;
using TestCertificate.Services;

namespace TestCertificate.Controllers;

[ApiController]
[Route("api/integrations/einvoice")]
public class EFacturaOAuthController : ControllerBase
{
    private readonly ITokenStore _tokenStore;
    private readonly IAnafOAuthClient _anafOAuthClient;
    private readonly ILogger<EFacturaOAuthController> _logger;
    private readonly AnafOAuthOptions _oauthOptions;

    public EFacturaOAuthController(
        ITokenStore tokenStore,
        IAnafOAuthClient anafOAuthClient,
        ILogger<EFacturaOAuthController> logger)
    {
        _tokenStore = tokenStore;
        _anafOAuthClient = anafOAuthClient;
        _logger = logger;
        _oauthOptions = new AnafOAuthOptions
        {
            ClientId = "2304bebb151ee370a47a34f5ffb92edd0c58d20f9b15ae68",
            ClientSecret = "c1ff456c384a9eb97918b9b69acf53098b5090627ea12edd0c58d20f9b15ae68",
            RedirectUri = "https://localhost:7205/api/integrations/einvoice/callback"
        };
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

            // Use RoEFactura OAuth client to generate the authorization URL
            var authUrl = _anafOAuthClient.GenerateAuthorizationUrl(_oauthOptions, state);

            _logger.LogInformation("Generated OAuth URL for client {ClientId}", _oauthOptions.ClientId);

            return Ok(new OAuthInitiateResponse
            {
                Success = true,
                AuthorizationUrl = authUrl,
                State = state
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate OAuth");
            return Ok(new OAuthInitiateResponse
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

            // Exchange code for token using RoEFactura OAuth client
            var anafToken = await _anafOAuthClient.ExchangeAuthorizationCodeAsync(code, _oauthOptions);

            // Convert RoEFactura Token to our storage format
            var token = new EFacturaToken
            {
                AccessToken = anafToken.AccessToken,
                RefreshToken = anafToken.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(anafToken.ExpiresIn),
                TokenType = anafToken.TokenType,
                Scope = anafToken.Scope,
                DebugInfo = new Dictionary<string, object>
                {
                    ["original_expires_in"] = anafToken.ExpiresIn,
                    ["received_at"] = DateTime.UtcNow,
                    ["source"] = "RoEFactura.AnafOAuthClient"
                }
            };

            _tokenStore.SaveToken("default", token);

            _logger.LogInformation("Token {AccessToken} successfully stored, expires at {ExpiresAt}", token.AccessToken, token.ExpiresAt);

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
            return Ok(new OAuthAuthorizationStatus
            {
                IsAuthorized = false
            });
        }

        return Ok(new OAuthAuthorizationStatus
        {
            IsAuthorized = true,
            ExpiresAt = token.ExpiresAt,
            TokenType = token.TokenType,
            AdditionalInfo = new Dictionary<string, object>
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
    public async Task<IActionResult> ExchangeCode([FromBody] OAuthCodeExchangeRequest request)
    {
        try
        {
            // Use RoEFactura OAuth client for token exchange
            var anafToken = await _anafOAuthClient.ExchangeAuthorizationCodeAsync(request.Code, _oauthOptions);

            // Store token
            var token = new EFacturaToken
            {
                AccessToken = anafToken.AccessToken,
                RefreshToken = anafToken.RefreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(anafToken.ExpiresIn),
                TokenType = anafToken.TokenType,
                Scope = anafToken.Scope,
                DebugInfo = new Dictionary<string, object>
                {
                    ["source"] = "RoEFactura.AnafOAuthClient.Manual"
                }
            };

            _tokenStore.SaveToken("default", token);

            return Ok(new { success = true, data = new {
                AccessToken = anafToken.AccessToken,
                TokenType = anafToken.TokenType,
                ExpiresIn = anafToken.ExpiresIn,
                Scope = anafToken.Scope
            }});
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Manual code exchange failed");
            return BadRequest(new { success = false, error = ex.Message });
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