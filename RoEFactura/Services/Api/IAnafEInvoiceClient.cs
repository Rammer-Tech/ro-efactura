using RoEFactura.Dtos;
using RoEFactura.Models;
using UblSharp;

namespace RoEFactura.Services.Api;

/// <summary>
/// Interface for ANAF e-invoice API client
/// </summary>
public interface IAnafEInvoiceClient
{
    /// <summary>
    /// Lists e-invoices from ANAF (non-paged)
    /// </summary>
    Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string filter = null);

    /// <summary>
    /// Lists e-invoices from ANAF with pagination
    /// </summary>
    Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(string token, long startMilliseconds, long endMilliseconds, string cui, string filter = null, int page = 1);

    /// <summary>
    /// Downloads an invoice to specified paths
    /// </summary>
    Task DownloadEInvoiceAsync(string token, string zipDestinationPath, string unzipDestinationPath, string eInvoiceDownloadId);

    /// <summary>
    /// Validates XML file
    /// </summary>
    Task<string> ValidateXmlAsync(string token, string xmlFilePath);

    /// <summary>
    /// Uploads XML file
    /// </summary>
    Task<string> UploadXmlAsync(string token, string xmlFilePath);

    /// <summary>
    /// Downloads and processes an invoice, returning the UBL invoice object
    /// </summary>
    Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId);

    /// <summary>
    /// Validates invoice XML content
    /// </summary>
    Task<ProcessingResult<InvoiceType>> ValidateInvoiceXmlAsync(string xmlContent);

    /// <summary>
    /// Processes multiple downloaded invoices in batch
    /// </summary>
    Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(string token, IEnumerable<string> eInvoiceDownloadIds);
}