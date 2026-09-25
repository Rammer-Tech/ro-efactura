using RoEFactura.Dtos;
using RoEFactura.Models;
using UblSharp;

namespace RoEFactura.Services.Api;

/// <summary>
/// Interface for ANAF e-invoice API client
/// </summary>
/// <remarks>
/// The list, upload, status and download members require a valid ANAF access token (Bearer JWT).
/// <see cref="ValidateWithAnafAsync"/> and <see cref="ConvertToPdfAsync"/> call ANAF's public, stateless
/// services and never send an Authorization header.
/// </remarks>
public interface IAnafEInvoiceClient
{
    /// <summary>
    /// Lists e-invoices from ANAF using the non-paged endpoint
    /// </summary>
    /// <remarks>
    /// The optional <paramref name="filter"/> value is passed through to ANAF as the "filtru" query parameter.
    /// </remarks>
    /// <example>
    /// <code>
    /// var items = await client.ListEInvoicesAsync(token.AccessToken, 30, "RO12345678");
    /// </code>
    /// </example>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="days">Number of days to look back for invoices (must be between 1 and 60)</param>
    /// <param name="cui">Romanian fiscal identification code (CUI/CIF) to filter invoices</param>
    /// <param name="filter">Optional filter parameter for additional invoice filtering</param>
    /// <returns>List of e-invoice responses from ANAF</returns>
    /// <exception cref="ArgumentException">Thrown when token, cui are null/empty or days is zero/negative</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when days is greater than 60</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string filter = null);

    /// <summary>
    /// Lists e-invoices from ANAF using the non-paged endpoint, with a required <see cref="CancellationToken"/>.
    /// </summary>
    /// <inheritdoc cref="ListEInvoicesAsync(string, int, string, string)"/>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="days">Number of days to look back for invoices (must be between 1 and 60)</param>
    /// <param name="cui">Romanian fiscal identification code (CUI/CIF) to filter invoices</param>
    /// <param name="filter">Optional filter parameter for additional invoice filtering</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(
        string token, int days, string cui, string? filter, CancellationToken cancellationToken);

    /// <summary>
    /// Lists e-invoices from ANAF using the paginated endpoint for efficient retrieval of large datasets
    /// </summary>
    /// <remarks>
    /// Use Unix timestamps in milliseconds for <paramref name="startMilliseconds"/> and <paramref name="endMilliseconds"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// long start = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
    /// long end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    /// var page = await client.ListPagedEInvoicesAsync(token.AccessToken, start, end, "RO12345678");
    /// </code>
    /// </example>
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
    /// Lists e-invoices from ANAF using the paginated endpoint, with a required <see cref="CancellationToken"/>.
    /// </summary>
    /// <inheritdoc cref="ListPagedEInvoicesAsync(string, long, long, string, string, int)"/>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="startMilliseconds">Start time as Unix timestamp in milliseconds (must be positive)</param>
    /// <param name="endMilliseconds">End time as Unix timestamp in milliseconds (must be positive)</param>
    /// <param name="cui">Romanian fiscal identification code (CUI/CIF) to filter invoices</param>
    /// <param name="filter">Optional filter parameter for additional invoice filtering</param>
    /// <param name="page">Page number for pagination (must be positive)</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(
        string token, long startMilliseconds, long endMilliseconds, string cui, string? filter, int page,
        CancellationToken cancellationToken);

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
    [Obsolete("Use DownloadMessageAsync; this member writes to disk.")]
    Task DownloadEInvoiceAsync(string token, string zipDestinationPath, string unzipDestinationPath, string eInvoiceDownloadId);

    /// <summary>
    /// Downloads an e-invoice from ANAF, extracts it, and parses it into a UBL InvoiceType object.
    /// RO_CIUS validation is skipped because invoices in SPV have already been validated by ANAF at upload time.
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="eInvoiceDownloadId">Unique identifier for the invoice to download and process</param>
    /// <returns>Processing result containing the parsed UBL InvoiceType object if successful, or parse errors if failed</returns>
    /// <exception cref="ArgumentException">Thrown when token or eInvoiceDownloadId are null or empty</exception>
    /// <exception cref="HttpRequestException">Thrown when the ANAF API request fails</exception>
    Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId);

    /// <summary>
    /// Downloads an e-invoice from ANAF and parses it, with a required <see cref="CancellationToken"/>.
    /// </summary>
    /// <inheritdoc cref="ProcessDownloadedInvoiceAsync(string, string)"/>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="eInvoiceDownloadId">Unique identifier for the invoice to download and process</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(
        string token, string eInvoiceDownloadId, CancellationToken cancellationToken);

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

    /// <summary>
    /// Downloads and processes multiple e-invoices from ANAF in batch, with a required <see cref="CancellationToken"/>.
    /// </summary>
    /// <inheritdoc cref="ProcessMultipleInvoicesAsync(string, IEnumerable{string})"/>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="eInvoiceDownloadIds">Collection of unique identifiers for invoices to download and process</param>
    /// <param name="cancellationToken">Token used to cancel the request; also checked between items</param>
    Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(
        string token, IEnumerable<string> eInvoiceDownloadIds, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads an XML invoice to ANAF (<c>upload</c> or, when <see cref="AnafUploadOptions.IsB2C"/> is set,
    /// <c>uploadb2c</c>) and returns the parsed response.
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="xml">Raw invoice XML bytes (max 10 MB)</param>
    /// <param name="options">Upload parameters: standard, CIF and the extern/autofactura/executare flags</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    /// <exception cref="ArgumentException">token or xml are null/empty, or xml exceeds the 10 MB limit</exception>
    /// <exception cref="ArgumentNullException">options is null</exception>
    /// <exception cref="AnafApiException">ANAF returned a non-2xx response</exception>
    /// <exception cref="AnafRateLimitException">ANAF returned HTTP 429</exception>
    Task<AnafUploadResult> UploadAsync(
        string token, byte[] xml, AnafUploadOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries the processing status of a previously uploaded message (<c>stareMesaj</c>).
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="uploadIndex">The upload index returned by <see cref="UploadAsync"/></param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    /// <exception cref="ArgumentException">token or uploadIndex are null/empty</exception>
    /// <exception cref="AnafApiException">ANAF returned a non-2xx response</exception>
    /// <exception cref="AnafRateLimitException">ANAF returned HTTP 429</exception>
    Task<AnafMessageStatusResult> GetMessageStatusAsync(
        string token, string uploadIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a message (<c>descarcare</c>) entirely in memory and splits the invoice/error XML from
    /// the Ministry of Finance signature XML.
    /// </summary>
    /// <param name="token">Bearer token for ANAF API authentication</param>
    /// <param name="downloadId">The download id (from <c>listaMesajeFactura</c> or <c>stareMesaj</c>)</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    /// <exception cref="ArgumentException">token or downloadId are null/empty</exception>
    /// <exception cref="AnafDownloadWindowExpiredException">The 60-day download window has passed</exception>
    /// <exception cref="AnafApiException">ANAF returned a non-2xx response, or a JSON error body</exception>
    /// <exception cref="AnafRateLimitException">ANAF returned HTTP 429</exception>
    Task<AnafDownloadResult> DownloadMessageAsync(
        string token, string downloadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an XML document against ANAF's public, stateless <c>validare</c> service. Never sends
    /// an Authorization header and always targets ANAF's public production services, regardless of the
    /// configured environment.
    /// </summary>
    /// <param name="xml">Raw XML bytes to validate (max 5 MB)</param>
    /// <param name="standard">Ubl → FACT1, CreditNote → FCN; no other value is accepted</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    /// <exception cref="ArgumentException">xml is null/empty, or exceeds the 5 MB limit</exception>
    /// <exception cref="ArgumentOutOfRangeException">standard is not Ubl or CreditNote</exception>
    /// <exception cref="AnafApiException">ANAF returned a non-2xx or unparseable response</exception>
    Task<AnafValidationResult> ValidateWithAnafAsync(
        byte[] xml, AnafDocumentStandard standard, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an XML document to PDF using ANAF's public, stateless <c>transformare</c> service. Never
    /// sends an Authorization header and always targets ANAF's public production services, regardless of
    /// the configured environment.
    /// </summary>
    /// <param name="xml">Raw XML bytes to convert (max 5 MB)</param>
    /// <param name="standard">Ubl → FACT1, CreditNote → FCN; no other value is accepted</param>
    /// <param name="validate">When false, appends the <c>/DA</c> segment and skips ANAF-side validation</param>
    /// <param name="cancellationToken">Token used to cancel the request</param>
    /// <returns>The PDF bytes</returns>
    /// <exception cref="ArgumentException">xml is null/empty, or exceeds the 5 MB limit</exception>
    /// <exception cref="ArgumentOutOfRangeException">standard is not Ubl or CreditNote</exception>
    /// <exception cref="AnafApiException">
    /// ANAF returned a non-2xx or unparseable response, or reported validation failures (see
    /// <see cref="AnafApiException.Errors"/>)
    /// </exception>
    Task<byte[]> ConvertToPdfAsync(
        byte[] xml, AnafDocumentStandard standard, bool validate = true, CancellationToken cancellationToken = default);
}
