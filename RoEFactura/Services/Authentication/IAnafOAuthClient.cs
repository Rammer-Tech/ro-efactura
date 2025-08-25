using System.Security.Cryptography.X509Certificates;

namespace RoEFactura.Services.Authentication;

/// <summary>
/// Interface for ANAF OAuth authentication client
/// </summary>
public interface IAnafOAuthClient
{
    /// <summary>
    /// Authenticates with ANAF using automatically discovered Romanian certificate
    /// </summary>
    Task<Token> GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl);

    /// <summary>
    /// Authenticates with ANAF using the specified certificate
    /// </summary>
    Task<Token> GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret, string callbackUrl);

    /// <summary>
    /// Authenticates with ANAF using certificate identified by thumbprint
    /// </summary>
    Task<Token> GetAccessTokenAsync(string thumbprint, string clientId, string clientSecret, string callbackUrl);
}