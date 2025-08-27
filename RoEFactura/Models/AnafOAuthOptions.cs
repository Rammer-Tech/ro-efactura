namespace RoEFactura.Models;

/// <summary>
/// Configuration options for ANAF OAuth integration
/// </summary>
public class AnafOAuthOptions
{
    /// <summary>
    /// ANAF OAuth Client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// ANAF OAuth Client Secret
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
    
    /// <summary>
    /// OAuth redirect URI (must be registered with ANAF)
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
    
    /// <summary>
    /// ANAF authorization endpoint URL
    /// Default: https://logincert.anaf.ro/anaf-oauth2/v1/authorize
    /// </summary>
    public string AuthorizeUrl { get; set; } = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize";
    
    /// <summary>
    /// ANAF token endpoint URL
    /// Default: https://logincert.anaf.ro/anaf-oauth2/v1/token
    /// </summary>
    public string TokenUrl { get; set; } = "https://logincert.anaf.ro/anaf-oauth2/v1/token";
    
    /// <summary>
    /// Whether to include token_content_type=jwt parameter
    /// Default: true (following SmartBill pattern)
    /// </summary>
    public bool IncludeTokenContentType { get; set; } = true;
    
    /// <summary>
    /// Validates the configuration
    /// </summary>
    public bool IsValid()
    {
        return !string.IsNullOrWhiteSpace(ClientId) &&
               !string.IsNullOrWhiteSpace(ClientSecret) &&
               !string.IsNullOrWhiteSpace(RedirectUri) &&
               !string.IsNullOrWhiteSpace(AuthorizeUrl) &&
               !string.IsNullOrWhiteSpace(TokenUrl);
    }
}