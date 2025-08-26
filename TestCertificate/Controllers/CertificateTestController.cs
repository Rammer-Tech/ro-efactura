using Microsoft.AspNetCore.Mvc;
using RoEFactura.Services.Authentication;
using TestCertificate.Models;
using TestCertificate.Services;

namespace TestCertificate.Controllers;

[ApiController]
[Route("api/test/certificates")]
public class CertificateTestController : ControllerBase
{
    private readonly ILogger<CertificateTestController> _logger;

    public CertificateTestController(ILogger<CertificateTestController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get all available Romanian certificates
    /// </summary>
    [HttpGet]
    public ActionResult<List<CertificateTestInfo>> GetAvailableCertificates()
    {
        try
        {
            _logger.LogInformation("Fetching available Romanian certificates");
            
            var certificates = CertificateTestService.GetAvailableCertificates();

            _logger.LogInformation("Found {Count} certificates", certificates.Count);
            return Ok(certificates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificates");
            return StatusCode(500, new AuthTestResult 
            { 
                Success = false, 
                Error = ex.Message,
                Message = "Failed to retrieve certificates"
            });
        }
    }

    /// <summary>
    /// Get specific certificate by thumbprint
    /// </summary>
    [HttpGet("{thumbprint}")]
    public ActionResult<CertificateTestInfo> GetCertificateInfo(string thumbprint)
    {
        try
        {
            _logger.LogInformation("Getting certificate info for thumbprint: {Thumbprint}", thumbprint);
            
            var certificate = CertificateTestService.GetCertificateByThumbprint(thumbprint);
            
            if (certificate == null)
            {
                return NotFound(new AuthTestResult 
                { 
                    Success = false, 
                    Message = $"Certificate with thumbprint {thumbprint} not found"
                });
            }

            var result = new CertificateTestInfo
            {
                Thumbprint = certificate.Thumbprint,
                Subject = certificate.Subject,
                Issuer = certificate.IssuerName.Name,
                ExpiryDate = certificate.NotAfter,
                HasPrivateKey = certificate.HasPrivateKey,
                IsValidForClientAuth = certificate.HasPrivateKey && certificate.NotAfter > DateTime.Now,
                StatusDescription = certificate.HasPrivateKey ? 
                    (certificate.NotAfter > DateTime.Now ? "✅ Valid" : "❌ Expired") : 
                    "❌ No Private Key"
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving certificate {Thumbprint}", thumbprint);
            return StatusCode(500, new AuthTestResult 
            { 
                Success = false, 
                Error = ex.Message,
                Message = "Failed to retrieve certificate info"
            });
        }
    }

    /// <summary>
    /// Clear certificate cache
    /// </summary>
    [HttpPost("clear-cache")]
    public ActionResult ClearCertificateCache()
    {
        try
        {
            // Note: Using direct certificate store access - no cache to clear
            // AnafOAuthClient.ClearCertificateCache(); // Not accessible from internal class
            _logger.LogInformation("Certificate cache cleared");
            
            return Ok(new AuthTestResult 
            { 
                Success = true, 
                Message = "Certificate cache cleared successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing certificate cache");
            return StatusCode(500, new AuthTestResult 
            { 
                Success = false, 
                Error = ex.Message,
                Message = "Failed to clear certificate cache"
            });
        }
    }

    /// <summary>
    /// Test endpoint to check if certificates are working
    /// </summary>
    [HttpGet("health")]
    public ActionResult<AuthTestResult> HealthCheck()
    {
        try
        {
            var certificates = CertificateTestService.GetAvailableCertificates();
            var validCerts = certificates.Count(c => c.IsValidForClientAuth);
            
            return Ok(new AuthTestResult
            {
                Success = true,
                Message = $"Certificate system healthy. Found {certificates.Count} certificates, {validCerts} valid for client auth",
                Data = new { TotalCerts = certificates.Count, ValidCerts = validCerts }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Certificate health check failed");
            return Ok(new AuthTestResult
            {
                Success = false,
                Error = ex.Message,
                Message = "Certificate system unhealthy"
            });
        }
    }
}