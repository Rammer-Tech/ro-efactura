using Microsoft.AspNetCore.Mvc;
using RoEFactura.Services.Authentication;
using System.Text.Json;
using TestCertificate.Models;

namespace TestCertificate.Controllers;

[ApiController]
[Route("api/test/anaf")]
public class AnafTestController : ControllerBase
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly ILogger<AnafTestController> _logger;

    public AnafTestController(IAnafOAuthClient anafClient, ILogger<AnafTestController> logger)
    {
        _anafClient = anafClient;
        _logger = logger;
    }

    /// <summary>
    /// Test ANAF authentication with auto-discovered certificate
    /// </summary>
    [HttpPost("authenticate")]
    public async Task<ActionResult<AuthTestResult>> TestAuthenticate([FromBody] AuthRequest request)
    {
        try
        {
            _logger.LogInformation("Testing ANAF authentication with auto-discovered certificate");
            
            var token = await _anafClient.GetAccessTokenAsync(
                request.ClientId,
                request.ClientSecret,
                request.CallbackUrl
            );

            return Ok(new AuthTestResult
            {
                Success = true,
                Message = "Authentication successful",
                Data = token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ANAF authentication failed");
            return Ok(new AuthTestResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Authentication failed"
            });
        }
    }

    /// <summary>
    /// Test ANAF authentication with specific certificate
    /// </summary>
    [HttpPost("authenticate/{thumbprint}")]
    public async Task<ActionResult<AuthTestResult>> TestAuthenticateWithCertificate(
        string thumbprint, 
        [FromBody] AuthRequest request)
    {
        try
        {
            _logger.LogInformation("Testing ANAF authentication with certificate {Thumbprint}", thumbprint);
            
            var token = await _anafClient.GetAccessTokenAsync(
                thumbprint,
                request.ClientId,
                request.ClientSecret,
                request.CallbackUrl
            );

            return Ok(new AuthTestResult
            {
                Success = true,
                Message = "Authentication with specific certificate successful",
                Data = token
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ANAF authentication with certificate {Thumbprint} failed", thumbprint);
            return Ok(new AuthTestResult
            {
                Success = false,
                Error = ex.Message,
                Message = $"Authentication with certificate {thumbprint} failed"
            });
        }
    }

    /// <summary>
    /// Mock OAuth token exchange (for testing without real ANAF)
    /// </summary>
    [HttpPost("token-exchange")]
    public async Task<ActionResult<AuthTestResult>> MockTokenExchange([FromBody] TokenExchangeRequest request)
    {
        try
        {
            _logger.LogInformation("Mock token exchange for code: {Code}", request.Code);

            // Simulate token exchange
            await Task.Delay(500); // Simulate network delay

            var mockToken = new
            {
                access_token = $"mock_token_{Guid.NewGuid():N}",
                token_type = "Bearer",
                expires_in = 3600,
                refresh_token = $"refresh_{Guid.NewGuid():N}",
                scope = "read"
            };

            return Ok(new AuthTestResult
            {
                Success = true,
                Message = "Mock token exchange successful",
                Data = mockToken
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mock token exchange failed");
            return Ok(new AuthTestResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Mock token exchange failed"
            });
        }
    }

    /// <summary>
    /// Generate OAuth authorization URL for testing
    /// </summary>
    [HttpPost("auth-url")]
    public ActionResult<AuthTestResult> GenerateAuthUrl([FromBody] AuthRequest request)
    {
        try
        {
            var authUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize?" +
                         $"response_type=code&" +
                         $"client_id={Uri.EscapeDataString(request.ClientId)}&" +
                         $"redirect_uri={Uri.EscapeDataString(request.CallbackUrl)}&" +
                         $"token_content_type=jwt";

            _logger.LogInformation("Generated auth URL for client {ClientId}", request.ClientId);

            return Ok(new AuthTestResult
            {
                Success = true,
                Message = "Authorization URL generated",
                Data = new { AuthUrl = authUrl, CallbackUrl = request.CallbackUrl }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate auth URL");
            return Ok(new AuthTestResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Failed to generate authorization URL"
            });
        }
    }

    /// <summary>
    /// Health check for ANAF test endpoints
    /// </summary>
    [HttpGet("health")]
    public ActionResult<AuthTestResult> HealthCheck()
    {
        return Ok(new AuthTestResult
        {
            Success = true,
            Message = "ANAF test endpoints are healthy",
            Data = new 
            { 
                Timestamp = DateTime.UtcNow,
                Endpoints = new[] 
                {
                    "/api/test/anaf/authenticate",
                    "/api/test/anaf/authenticate/{thumbprint}",
                    "/api/test/anaf/token-exchange",
                    "/api/test/anaf/auth-url"
                }
            }
        });
    }
}