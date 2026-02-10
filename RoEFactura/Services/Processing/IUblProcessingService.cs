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
    /// <param name="xmlData">Raw XML bytes to process</param>
    /// <param name="fileName">File name for logging purposes</param>
    /// <param name="skipValidation">When true, skips RO_CIUS validation (e.g. for invoices already validated by ANAF SPV)</param>
    Task<ProcessingResult<InvoiceType>> ProcessInvoiceXmlAsync(byte[] xmlData, string fileName, bool skipValidation = false);

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