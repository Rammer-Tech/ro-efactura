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
    /// Lists e-invoices from ANAF using the non-paged endpoint
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="days">Number of days to look back for invoices (must be positive)</param>
    /// <param name="cui">Romanian fiscal identification code (CUI/CIF) to filter invoices</param>
    /// <param name="filter">Optional filter parameter for additional invoice filtering</param>
    /// <returns>List of e-invoice responses from ANAF</returns>
    /// <exception cref="ArgumentException">Thrown when token, cui are null/empty or days is zero/negative</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string filter = null);

    /// <summary>
    /// Lists e-invoices from ANAF using the paginated endpoint for efficient retrieval of large datasets
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="startMilliseconds">Start time as Unix timestamp in milliseconds (must be positive)</param>
    /// <param name="endMilliseconds">End time as Unix timestamp in milliseconds (must be positive)</param>
    /// <param name="cui">Romanian fiscal identification code (CUI/CIF) to filter invoices</param>
    /// <param name="filter">Optional filter parameter for additional invoice filtering</param>
    /// <param name="page">Page number for pagination (must be positive, defaults to 1)</param>
    /// <returns>Paginated response containing e-invoices and pagination metadata</returns>
    /// <exception cref="ArgumentException">Thrown when token, cui are null/empty or timestamps/page are zero/negative</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(string token, long startMilliseconds, long endMilliseconds, string cui, string filter = null, int page = 1);

    /// <summary>
    /// Downloads an e-invoice as a ZIP file from ANAF and extracts it to the specified paths
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="zipDestinationPath">Directory path where the downloaded ZIP file will be saved (created if not exists)</param>
    /// <param name="unzipDestinationPath">Directory path where the ZIP contents will be extracted (created if not exists)</param>
    /// <param name="eInvoiceDownloadId">Unique identifier for the invoice to download from ANAF</param>
    /// <exception cref="ArgumentException">Thrown when any parameter is null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    /// <exception cref="IOException">Thrown when file operations fail</exception>
    Task DownloadEInvoiceAsync(string token, string zipDestinationPath, string unzipDestinationPath, string eInvoiceDownloadId);

    /// <summary>
    /// Validates an XML invoice file against ANAF validation rules
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="xmlFilePath">Absolute path to the XML file to validate</param>
    /// <returns>Validation response from ANAF API indicating success or validation errors</returns>
    /// <exception cref="ArgumentException">Thrown when token or xmlFilePath are null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified XML file does not exist</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<string> ValidateXmlAsync(string token, string xmlFilePath);

    /// <summary>
    /// Validates XML invoice content against ANAF validation rules without requiring a physical file
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="xmlContent">XML content as string to validate</param>
    /// <param name="fileName">Optional filename to use in the multipart form (defaults to "invoice.xml")</param>
    /// <returns>Validation response from ANAF API indicating success or validation errors</returns>
    /// <exception cref="ArgumentException">Thrown when token, xmlContent, or fileName are null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<string> ValidateXmlContentAsync(string token, string xmlContent, string fileName = "invoice.xml");

    /// <summary>
    /// Uploads an XML invoice file to the ANAF e-invoice system
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="xmlFilePath">Absolute path to the XML file to upload</param>
    /// <returns>Upload response from ANAF API with status and processing information</returns>
    /// <exception cref="ArgumentException">Thrown when token or xmlFilePath are null or empty</exception>
    /// <exception cref="FileNotFoundException">Thrown when the specified XML file does not exist</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<string> UploadXmlAsync(string token, string xmlFilePath);

    /// <summary>
    /// Uploads XML invoice content to the ANAF e-invoice system without requiring a physical file
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="xmlContent">XML content as string to upload</param>
    /// <param name="fileName">Optional filename to use in the multipart form (defaults to "invoice.xml")</param>
    /// <returns>Upload response from ANAF API with status and processing information</returns>
    /// <exception cref="ArgumentException">Thrown when token, xmlContent, or fileName are null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<string> UploadXmlContentAsync(string token, string xmlContent, string fileName = "invoice.xml");

    /// <summary>
    /// Downloads an e-invoice from ANAF, extracts it, and processes it through the UBL validation pipeline
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="eInvoiceDownloadId">Unique identifier for the invoice to download and process</param>
    /// <returns>Processing result containing the UBL InvoiceType object if successful, or validation errors if failed</returns>
    /// <exception cref="ArgumentException">Thrown when token or eInvoiceDownloadId are null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId);

    /// <summary>
    /// Validates UBL XML invoice content against Romanian RO_CIUS validation rules locally (no ANAF API call)
    /// </summary>
    /// <param name="xmlContent">XML content as string containing UBL invoice data</param>
    /// <returns>Processing result containing the parsed UBL InvoiceType object if validation passes, or detailed validation errors if failed</returns>
    /// <exception cref="ArgumentException">Thrown when xmlContent is null or empty</exception>
    Task<ProcessingResult<InvoiceType>> ValidateInvoiceXmlAsync(string xmlContent);

    /// <summary>
    /// Downloads and processes multiple e-invoices from ANAF in batch, returning individual processing results
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="eInvoiceDownloadIds">Collection of unique identifiers for invoices to download and process</param>
    /// <returns>List of processing results, one for each invoice, containing UBL objects or validation errors</returns>
    /// <exception cref="ArgumentException">Thrown when token is null/empty or eInvoiceDownloadIds is null</exception>
    /// <exception cref="HttpRequestException">Thrown when ANAF API requests fail</exception>
    Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(string token, IEnumerable<string> eInvoiceDownloadIds);
}