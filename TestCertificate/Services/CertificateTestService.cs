using System.Security.Cryptography.X509Certificates;
using RoEFactura.Services.Authentication;
using TestCertificate.Models;

namespace TestCertificate.Services;

/// <summary>
/// Service to access certificate functionality for testing
/// This is needed because AnafOAuthClient static methods are not accessible from internal class
/// </summary>
public class CertificateTestService
{
    /// <summary>
    /// Get available Romanian certificates using reflection or alternative approach
    /// </summary>
    public static List<CertificateTestInfo> GetAvailableCertificates()
    {
        try
        {
            // Access Windows Certificate Store directly since we can't access AnafOAuthClient static methods
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            try
            {
                X509Certificate2Collection validCertificates =
                    store.Certificates.Find(X509FindType.FindByTimeValid, DateTime.Now, validOnly: false);

                var romanianCertificates = new List<CertificateTestInfo>();

                foreach (X509Certificate2 cert in validCertificates)
                {
                    string certificateIssuerName = cert.IssuerName.Name;

                    bool isRomanianCA = 
                        certificateIssuerName.Contains("CERTSIGN", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("DIGISIGN", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("TRAN", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("ALFASIGN", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("CERT DIGI", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("CERTDIGITAL", StringComparison.CurrentCultureIgnoreCase) ||
                        certificateIssuerName.Contains("DE CALCUL", StringComparison.CurrentCultureIgnoreCase);

                    if (isRomanianCA)
                    {
                        romanianCertificates.Add(new CertificateTestInfo
                        {
                            Thumbprint = cert.Thumbprint,
                            Subject = cert.Subject,
                            Issuer = cert.IssuerName.Name,
                            ExpiryDate = cert.NotAfter,
                            HasPrivateKey = cert.HasPrivateKey,
                            IsValidForClientAuth = IsValidForClientAuthentication(cert),
                            StatusDescription = GetStatusDescription(cert)
                        });
                    }
                }

                return romanianCertificates.OrderByDescending(c => c.IsValidForClientAuth)
                                          .ThenBy(c => c.ExpiryDate)
                                          .ToList();
            }
            finally
            {
                store.Close();
            }
        }
        catch (Exception)
        {
            return new List<CertificateTestInfo>();
        }
    }

    /// <summary>
    /// Get certificate by thumbprint
    /// </summary>
    public static X509Certificate2? GetCertificateByThumbprint(string thumbprint)
    {
        try
        {
            X509Store store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

            try
            {
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);
                return found.Count > 0 ? found[0] : null;
            }
            finally
            {
                store.Close();
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Validate certificate for client authentication
    /// </summary>
    private static bool IsValidForClientAuthentication(X509Certificate2 certificate)
    {
        try
        {
            // Check if certificate has a private key (required for client authentication)
            if (!certificate.HasPrivateKey)
            {
                return false;
            }

            // Check Enhanced Key Usage extension for Client Authentication
            var extensions = certificate.Extensions;
            foreach (X509Extension extension in extensions)
            {
                if (extension.Oid?.Value == "2.5.29.37") // Enhanced Key Usage OID
                {
                    var eku = extension as X509EnhancedKeyUsageExtension;
                    if (eku != null)
                    {
                        // Check for Client Authentication OID (1.3.6.1.5.5.7.3.2)
                        foreach (var oid in eku.EnhancedKeyUsages)
                        {
                            if (oid.Value == "1.3.6.1.5.5.7.3.2") // Client Authentication
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            // If no Enhanced Key Usage extension found, check Key Usage
            foreach (X509Extension extension in extensions)
            {
                if (extension.Oid?.Value == "2.5.29.15") // Key Usage OID
                {
                    var ku = extension as X509KeyUsageExtension;
                    if (ku != null)
                    {
                        // Digital Signature and Key Encipherment are required for client auth
                        return ku.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature) ||
                               ku.KeyUsages.HasFlag(X509KeyUsageFlags.KeyEncipherment);
                    }
                }
            }

            // If no specific usage extensions found, assume it's valid (fallback)
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get status description for certificate
    /// </summary>
    private static string GetStatusDescription(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey) return "❌ No Private Key";
        if (!IsValidForClientAuthentication(certificate)) return "⚠️ Not for Client Auth";
        if (certificate.NotAfter <= DateTime.Now) return "❌ Expired";
        if (certificate.NotAfter <= DateTime.Now.AddDays(30)) return "⚠️ Expires Soon";
        return "✅ Valid";
    }
}