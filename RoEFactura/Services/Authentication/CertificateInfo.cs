using System.Security.Cryptography.X509Certificates;

namespace RoEFactura.Services.Authentication;

/// <summary>
/// Information about a certificate available for ANAF authentication
/// </summary>
public class CertificateInfo
{
    /// <summary>
    /// The actual X509Certificate2 object
    /// </summary>
    public required X509Certificate2 Certificate { get; set; }

    /// <summary>
    /// Certificate subject name (typically contains the owner's name)
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// Certificate issuer (Certificate Authority name)
    /// </summary>
    public required string Issuer { get; set; }

    /// <summary>
    /// Certificate expiration date
    /// </summary>
    public required DateTime ExpiryDate { get; set; }

    /// <summary>
    /// Whether the certificate has an associated private key
    /// </summary>
    public required bool HasPrivateKey { get; set; }

    /// <summary>
    /// Whether the certificate is valid for client authentication
    /// </summary>
    public required bool IsValidForClientAuth { get; set; }

    /// <summary>
    /// Certificate thumbprint (unique identifier)
    /// </summary>
    public required string Thumbprint { get; set; }

    /// <summary>
    /// User-friendly display name for the certificate
    /// </summary>
    public string DisplayName => $"{ExtractCommonName(Subject)} - {ExtractIssuerCA(Issuer)} (Expires: {ExpiryDate:yyyy-MM-dd})";

    /// <summary>
    /// Certificate validity status description
    /// </summary>
    public string StatusDescription
    {
        get
        {
            if (!HasPrivateKey) return "❌ No Private Key";
            if (!IsValidForClientAuth) return "⚠️ Not for Client Auth";
            if (ExpiryDate <= DateTime.Now) return "❌ Expired";
            if (ExpiryDate <= DateTime.Now.AddDays(30)) return "⚠️ Expires Soon";
            return "✅ Valid";
        }
    }

    /// <summary>
    /// Whether this certificate is recommended for use
    /// </summary>
    public bool IsRecommended => HasPrivateKey && IsValidForClientAuth && ExpiryDate > DateTime.Now.AddDays(7);

    private static string ExtractCommonName(string subject)
    {
        // Extract CN (Common Name) from subject
        var parts = subject.Split(',');
        var cnPart = parts.FirstOrDefault(p => p.Trim().StartsWith("CN="));
        return cnPart?.Substring(3).Trim() ?? "Unknown";
    }

    private static string ExtractIssuerCA(string issuer)
    {
        // Extract and simplify issuer name
        if (issuer.Contains("CERTSIGN")) return "CertSign";
        if (issuer.Contains("DIGISIGN")) return "DigiSign";
        if (issuer.Contains("ALFASIGN")) return "AlfaSign";
        if (issuer.Contains("CERTDIGITAL") || issuer.Contains("CERT DIGI")) return "CertDigital";
        if (issuer.Contains("TRAN")) return "TransSigne";
        
        // Fallback: extract O (Organization) from issuer
        var parts = issuer.Split(',');
        var oPart = parts.FirstOrDefault(p => p.Trim().StartsWith("O="));
        return oPart?.Substring(2).Trim() ?? "Unknown CA";
    }
}