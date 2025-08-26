namespace TestCertificate.Models;

public class EFacturaAuthRequest
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RedirectUri { get; set; }
}

public class EFacturaTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = "Bearer";
    public string? Scope { get; set; }
}

public class EFacturaAuthStatus
{
    public bool IsAuthorized { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? TokenType { get; set; }
    public TimeSpan? ExpiresIn => ExpiresAt.HasValue ? ExpiresAt.Value - DateTime.UtcNow : null;
    public Dictionary<string, object>? TokenInfo { get; set; }
}

public class EFacturaInitiateResponse
{
    public bool Success { get; set; }
    public string? AuthUrl { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
}

public class EFacturaCodeExchangeRequest
{
    public string Code { get; set; } = string.Empty;
    public string? State { get; set; }
}