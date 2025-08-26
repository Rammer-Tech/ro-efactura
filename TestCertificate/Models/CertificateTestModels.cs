namespace TestCertificate.Models;

public class AuthRequest
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string CallbackUrl { get; set; } = "";
}

public class TokenExchangeRequest
{
    public string Code { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}

public class CertificateTestInfo
{
    public string Thumbprint { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Issuer { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public bool HasPrivateKey { get; set; }
    public bool IsValidForClientAuth { get; set; }
    public string StatusDescription { get; set; } = "";
}

public class AuthTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = "";
    public object? Data { get; set; }
    public string? Error { get; set; }
}