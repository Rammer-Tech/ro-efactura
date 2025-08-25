using RoEFactura.Models;
using UblSharp;

namespace RoEFactura.Services.Processing;

/// <summary>
/// Interface for UBL document processing service
/// </summary>
public interface IUblProcessingService
{
    /// <summary>
    /// Processes UBL XML content and returns structured result
    /// </summary>
    Task<ProcessingResult<InvoiceType>> ProcessInvoiceXmlAsync(byte[] xmlData, string fileName);

    /// <summary>
    /// Validates UBL invoice against Romanian RO_CIUS rules
    /// </summary>
    Task<ProcessingResult<InvoiceType>> ValidateInvoiceAsync(InvoiceType invoice);

    /// <summary>
    /// Extracts invoice from ZIP archive and processes it
    /// </summary>
    Task<ProcessingResult<InvoiceType>> ProcessInvoiceZipAsync(byte[] zipData, string fileName);

    /// <summary>
    /// Gets processing statistics for monitoring
    /// </summary>
    ProcessingStats GetProcessingStats();

    /// <summary>
    /// Resets processing statistics
    /// </summary>
    void ResetProcessingStats();
}