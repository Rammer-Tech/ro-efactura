using System.Security.Cryptography.X509Certificates;
using RoEFactura.Models;

namespace RoEFactura.Services.Authentication;

/// <summary>
/// Interface for ANAF OAuth authentication client
/// </summary>
public interface IAnafOAuthClient
{
    // ===== Certificate-based authentication (for desktop/server apps) =====
    
    /// <summary>
    /// Authenticates with ANAF using automatically discovered Romanian certificate
    /// </summary>
    public Task<Token> GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl);

    /// <summary>
    /// Authenticates with ANAF using the specified certificate
    /// </summary>
    public Task<Token> GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret, string callbackUrl);

    /// <summary>
    /// Authenticates with ANAF using certificate identified by thumbprint
    /// </summary>
    public Task<Token> GetAccessTokenAsync(string thumbprint, string clientId, string clientSecret, string callbackUrl);
    
    // ===== OAuth redirect flow (for web apps) =====
    
    /// <summary>
    /// Generates the OAuth authorization URL for redirecting users to ANAF
    /// </summary>
    /// <param name="clientId">OAuth client ID</param>
    /// <param name="redirectUri">Redirect URI (must be registered with ANAF)</param>
    /// <param name="state">Optional state parameter for CSRF protection</param>
    /// <returns>The authorization URL to redirect the user to</returns>
    public string GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null);
    
    /// <summary>
    /// Generates the OAuth authorization URL using configured options
    /// </summary>
    /// <param name="options">OAuth configuration options</param>
    /// <param name="state">Optional state parameter for CSRF protection</param>
    /// <returns>The authorization URL to redirect the user to</returns>
    public string GenerateAuthorizationUrl(AnafOAuthOptions options, string? state = null);

    /// <summary>
    /// Exchanges an authorization code for access token
    /// </summary>
    /// <param name="code">Authorization code received from ANAF callback</param>
    /// <param name="clientId">OAuth client ID</param>
    /// <param name="clientSecret">OAuth client secret</param>
    /// <param name="redirectUri">Redirect URI (must match the one used in authorization)</param>
    /// <returns>Token response from ANAF</returns>
    public Task<Token> ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri);
    
    /// <summary>
    /// Exchanges an authorization code for access token using configured options
    /// </summary>
    /// <param name="code">Authorization code received from ANAF callback</param>
    /// <param name="options">OAuth configuration options</param>
    /// <returns>Token response from ANAF</returns>
    public Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options);
}