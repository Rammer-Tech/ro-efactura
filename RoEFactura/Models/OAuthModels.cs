namespace RoEFactura.Models;

/// <summary>
/// Response from OAuth authorization initiation
/// </summary>
public class OAuthInitiateResponse
{
    public bool Success { get; set; }
    public string? AuthorizationUrl { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// OAuth token response from ANAF
/// </summary>
public class OAuthTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string? Scope { get; set; }
}

/// <summary>
/// OAuth authorization status
/// </summary>
public class OAuthAuthorizationStatus
{
    public bool IsAuthorized { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? TokenType { get; set; }
    public TimeSpan? ExpiresIn => ExpiresAt.HasValue ? ExpiresAt.Value - DateTime.UtcNow : null;
    public Dictionary<string, object>? AdditionalInfo { get; set; }
}

/// <summary>
/// OAuth code exchange request
/// </summary>
public class OAuthCodeExchangeRequest
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
    public string? RedirectUri { get; set; }
}