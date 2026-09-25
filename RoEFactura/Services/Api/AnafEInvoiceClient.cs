using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoEFactura.Dtos;
using RoEFactura.Extensions;
using RoEFactura.Models;
using RoEFactura.Services.Processing;
using RoEFactura.Utilities;
using UblSharp;

namespace RoEFactura.Services.Api;

internal sealed class AnafEInvoiceClient : IAnafEInvoiceClient
{
    private const int MaxUploadBytes = 10 * 1024 * 1024;
    private const int MaxPublicServiceBytes = 5 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IUblProcessingService _ublProcessingService;
    private readonly ILogger<AnafEInvoiceClient> _logger;
    private readonly string _apiBase;
    private readonly string _publicBase;

    public AnafEInvoiceClient(
        HttpClient httpClient,
        IOptions<RoEFacturaOptions> options,
        IUblProcessingService ublProcessingService,
        ILogger<AnafEInvoiceClient> logger)
    {
        _httpClient = httpClient;
        _ublProcessingService = ublProcessingService;
        _logger = logger;
        _apiBase = options.Value.ResolveApiBaseUrl();
        _publicBase = options.Value.ResolvePublicServicesBaseUrl();
    }

    /// <summary>
    ///     Send + structured-log an ANAF HTTP exchange. Logs the request URL/method/token-fingerprint
    ///     before sending and the response status/content-type/content-length/content-disposition on
    ///     return. On non-success, also logs a truncated body preview, but only for JSON/XML bodies.
    ///     Request content (XML, form bodies) is never logged.
    /// </summary>
    /// <param name="op">Short operation name used as a log prefix (e.g. "list", "download").</param>
    /// <param name="request">Fully-formed request. Authorization header is logged as a fingerprint only.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    private async Task<HttpResponseMessage> SendAndLogAsync(
        string op, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url = request.RequestUri?.ToString() ?? "(no uri)";
        string method = request.Method.Method;
        string tokenFingerprint = "(none)";
        if (request.Headers.Authorization is { Scheme: "Bearer", Parameter: { Length: > 12 } token })
            tokenFingerprint = $"Bearer {token[..8]}…{token[^4..]} (len={token.Length})";

        _logger.LogInformation(
            "ANAF[{Op}] → {Method} {Url} auth={TokenFingerprint}",
            op, method, url, tokenFingerprint);

        HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);

        string mediaType = response.Content.Headers.ContentType?.MediaType ?? "(none)";
        long? contentLength = response.Content.Headers.ContentLength;
        bool hasContentDisposition = response.Content.Headers.ContentDisposition != null;

        _logger.LogInformation(
            "ANAF[{Op}] ← {Status} {Reason} content-type={MediaType} content-length={Length} content-disposition={HasContentDisposition}",
            op, (int)response.StatusCode, response.ReasonPhrase, mediaType, contentLength, hasContentDisposition);

        if (!response.IsSuccessStatusCode
            && (mediaType.Contains("json", StringComparison.OrdinalIgnoreCase)
                || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)))
        {
            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > 300)
                body = body[..300] + "…";
            _logger.LogWarning("ANAF[{Op}] non-success body: {Body}", op, body);
        }

        return response;
    }

    /// <summary>
    /// Throws a typed exception for a non-2xx response. Used by every call, list endpoints included.
    /// </summary>
    private static async Task ThrowForStatusAsync(string op, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        string raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta
                ?? (response.Headers.RetryAfter?.Date is { } retryDate ? retryDate - DateTimeOffset.UtcNow : null);
            throw new AnafRateLimitException(retryAfter, raw);
        }

        throw new AnafApiException(
            response.StatusCode,
            $"ANAF {op} returned {(int)response.StatusCode} {response.ReasonPhrase}.",
            raw);
    }

    private static string BuildQuery(params (string Key, string? Value)[] pairs)
    {
        IEnumerable<string> parts = pairs
            .Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        return string.Join("&", parts);
    }

    private static string MapUploadStandard(AnafDocumentStandard standard) => standard switch
    {
        AnafDocumentStandard.Ubl => "UBL",
        AnafDocumentStandard.CreditNote => "CN",
        AnafDocumentStandard.Cii => "CII",
        AnafDocumentStandard.Rasp => "RASP",
        _ => throw new ArgumentOutOfRangeException(nameof(standard), standard, "Unsupported upload standard.")
    };

    private static string MapPublicServiceStandard(AnafDocumentStandard standard) => standard switch
    {
        AnafDocumentStandard.Ubl => "FACT1",
        AnafDocumentStandard.CreditNote => "FCN",
        _ => throw new ArgumentOutOfRangeException(
            nameof(standard), standard, "ANAF validation/PDF conversion only accepts Ubl or CreditNote.")
    };

    private static bool IsZip(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x50 && bytes[1] == 0x4B && bytes[2] == 0x03 && bytes[3] == 0x04;

    /// <inheritdoc/>
    public Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string filter = null)
        => ListEInvoicesAsync(token, days, cui, filter, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(
        string token, int days, string cui, string? filter, CancellationToken cancellationToken)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        cui = Guard.Against.NullOrWhiteSpace(cui);
        days = Guard.Against.NegativeOrZero(days);
        if (days > 60)
            throw new ArgumentOutOfRangeException(nameof(days), days, "days must be between 1 and 60.");
        string normalizedCui = CifNormalizer.Normalize(cui);

        string query = BuildQuery(
            ("zile", days.ToString(CultureInfo.InvariantCulture)),
            ("cif", normalizedCui),
            ("filtru", string.IsNullOrWhiteSpace(filter) ? null : filter));
        string url = $"{_apiBase}/listaMesajeFactura?{query}";

        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await SendAndLogAsync("list", request, cancellationToken);
        await ThrowForStatusAsync("list", response, cancellationToken);

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (AnafResponseParser.TryParseEroare(content, out string? eroare, out _))
        {
            // ANAF uses the same {"eroare": ...} shape both for "no messages in this interval" and for
            // real errors (e.g. no query right for this CIF, an invalid CIF). Only the former is a
            // legitimately empty result; anything else must not be silently swallowed into `[]`, or a
            // sync process would read "no invoices" instead of an actionable error.
            if (eroare!.StartsWith("Nu exista mesaje", StringComparison.OrdinalIgnoreCase))
                return [];

            throw new AnafApiException(HttpStatusCode.OK, eroare, content, [eroare]);
        }

        ListEInvoicesAnafResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<ListEInvoicesAnafResponse>(content, AnafJson.Options);
        }
        catch (JsonException)
        {
            throw new AnafApiException(HttpStatusCode.OK, "Unexpected ANAF list response.", content);
        }

        result ??= new ListEInvoicesAnafResponse();
        result.Items ??= [];
        return result.Items;
    }

    /// <inheritdoc/>
    public Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(
        string token, long startMilliseconds, long endMilliseconds, string cui, string filter = null, int page = 1)
        => ListPagedEInvoicesAsync(token, startMilliseconds, endMilliseconds, cui, filter, page, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(
        string token, long startMilliseconds, long endMilliseconds, string cui, string? filter, int page,
        CancellationToken cancellationToken)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        cui = Guard.Against.NullOrWhiteSpace(cui);
        startMilliseconds = Guard.Against.NegativeOrZero(startMilliseconds);
        endMilliseconds = Guard.Against.NegativeOrZero(endMilliseconds);
        page = Guard.Against.NegativeOrZero(page);
        string normalizedCui = CifNormalizer.Normalize(cui);

        string query = BuildQuery(
            ("startTime", startMilliseconds.ToString(CultureInfo.InvariantCulture)),
            ("endTime", endMilliseconds.ToString(CultureInfo.InvariantCulture)),
            ("cif", normalizedCui),
            ("pagina", page.ToString(CultureInfo.InvariantCulture)),
            ("filtru", string.IsNullOrWhiteSpace(filter) ? null : filter));
        string url = $"{_apiBase}/listaMesajePaginatieFactura?{query}";

        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await SendAndLogAsync("list-paged", request, cancellationToken);
        await ThrowForStatusAsync("list-paged", response, cancellationToken);

        string content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (AnafResponseParser.TryParseEroare(content, out string? eroare, out string? titlu))
        {
            return new EInvoiceAnafPagedListResponse
            {
                Items = [],
                Error = eroare,
                Title = titlu
            };
        }

        EInvoiceAnafPagedListResponse? result;
        try
        {
            result = JsonSerializer.Deserialize<EInvoiceAnafPagedListResponse>(content, AnafJson.Options);
        }
        catch (JsonException)
        {
            throw new AnafApiException(HttpStatusCode.OK, "Unexpected ANAF list response.", content);
        }

        result ??= new EInvoiceAnafPagedListResponse();
        result.Items ??= [];
        return result;
    }

    /// <inheritdoc/>
    [Obsolete("Use DownloadMessageAsync; this member writes to disk.")]
    public async Task DownloadEInvoiceAsync(
        string token, string zipDestinationPath, string unzipDestinationPath, string eInvoiceDownloadId)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        zipDestinationPath = Guard.Against.NullOrWhiteSpace(zipDestinationPath);
        unzipDestinationPath = Guard.Against.NullOrWhiteSpace(unzipDestinationPath);
        eInvoiceDownloadId = Guard.Against.NullOrWhiteSpace(eInvoiceDownloadId);

        AnafDownloadResult result = await DownloadMessageAsync(token, eInvoiceDownloadId, CancellationToken.None);

        if (!Directory.Exists(zipDestinationPath))
            Directory.CreateDirectory(zipDestinationPath);

        if (!Directory.Exists(unzipDestinationPath))
            Directory.CreateDirectory(unzipDestinationPath);

        string zipFilePath = Path.Combine(zipDestinationPath, result.FileName);
        await File.WriteAllBytesAsync(zipFilePath, result.ZipContent);

        _logger.LogDebug("Downloaded e-invoice to {ZipFilePath}", zipFilePath);

        ZipFile.ExtractToDirectory(zipFilePath, unzipDestinationPath);

        _logger.LogDebug("Extracted e-invoice to {UnzipDestinationPath}", unzipDestinationPath);
    }

    /// <inheritdoc/>
    public Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId)
        => ProcessDownloadedInvoiceAsync(token, eInvoiceDownloadId, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(
        string token, string eInvoiceDownloadId, CancellationToken cancellationToken)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        eInvoiceDownloadId = Guard.Against.NullOrWhiteSpace(eInvoiceDownloadId);

        try
        {
            _logger.LogInformation("Processing downloaded invoice with ID: {DownloadId}", eInvoiceDownloadId);

            AnafDownloadResult downloaded = await DownloadMessageAsync(token, eInvoiceDownloadId, cancellationToken);

            if (downloaded.DocumentXml == null)
            {
                _logger.LogWarning(
                    "No invoice XML in downloaded invoice {DownloadId} (missing, or only semnatura XML)",
                    eInvoiceDownloadId);
                return ProcessingResult<InvoiceType>.Failed(
                    "No invoice XML found in downloaded invoice (only semnatura sidecars or no XML)");
            }

            ProcessingResult<InvoiceType> result = await _ublProcessingService.ProcessInvoiceXmlAsync(
                downloaded.DocumentXml, eInvoiceDownloadId, skipValidation: true);

            if (result.IsSuccess && result.Data != null)
            {
                _logger.LogInformation(
                    "Successfully processed downloaded invoice {DownloadId} from ANAF", eInvoiceDownloadId);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AnafDownloadWindowExpiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing downloaded invoice {DownloadId}", eInvoiceDownloadId);
            return ProcessingResult<InvoiceType>.Failed($"Processing error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<ProcessingResult<InvoiceType>> ValidateInvoiceXmlAsync(string xmlContent)
    {
        Guard.Against.NullOrWhiteSpace(xmlContent);

        try
        {
            _logger.LogInformation("Validating invoice XML content");

            InvoiceType? ublInvoice = UblSharpExtensions.LoadInvoiceFromXml(xmlContent);
            if (ublInvoice == null)
            {
                return ProcessingResult<InvoiceType>.Failed("Failed to parse UBL XML content");
            }

            ProcessingResult<InvoiceType> result = await _ublProcessingService.ValidateInvoiceAsync(ublInvoice);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Invoice XML validation successful");
            }
            else
            {
                _logger.LogWarning("Invoice XML validation failed: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating invoice XML");
            return ProcessingResult<InvoiceType>.Failed($"Validation error: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(
        string token, IEnumerable<string> eInvoiceDownloadIds)
        => ProcessMultipleInvoicesAsync(token, eInvoiceDownloadIds, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(
        string token, IEnumerable<string> eInvoiceDownloadIds, CancellationToken cancellationToken)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        List<string> invoiceDownloadIds = eInvoiceDownloadIds.ToList();
        Guard.Against.Null(invoiceDownloadIds);

        List<ProcessingResult<InvoiceType>> results = new();

        _logger.LogInformation("Processing {Count} invoices in batch", invoiceDownloadIds.Count);

        foreach (string downloadId in invoiceDownloadIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ProcessingResult<InvoiceType> result =
                    await ProcessDownloadedInvoiceAsync(token, downloadId, cancellationToken);
                results.Add(result);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Successfully processed invoice from batch");
                }
                else
                {
                    _logger.LogWarning("Failed to process invoice {DownloadId}: {Errors}",
                        downloadId, string.Join(", ", result.Errors.Select(e => e.ErrorMessage)));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing invoice {DownloadId} in batch", downloadId);
                results.Add(ProcessingResult<InvoiceType>.Failed($"Processing error: {ex.Message}"));
            }
        }

        int successCount = results.Count(r => r.IsSuccess);
        _logger.LogInformation("Batch processing completed: {Success}/{Total} invoices processed successfully",
            successCount, results.Count);

        return results;
    }

    /// <inheritdoc/>
    public async Task<AnafUploadResult> UploadAsync(
        string token, byte[] xml, AnafUploadOptions options, CancellationToken cancellationToken = default)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        if (xml == null || xml.Length == 0)
            throw new ArgumentException("xml must not be null or empty.", nameof(xml));
        if (xml.Length > MaxUploadBytes)
            throw new ArgumentException($"xml exceeds the maximum upload size of {MaxUploadBytes} bytes.", nameof(xml));
        ArgumentNullException.ThrowIfNull(options);
        string normalizedCif = CifNormalizer.Normalize(options.Cif);

        string standardValue = MapUploadStandard(options.Standard);
        string path = options.IsB2C ? "uploadb2c" : "upload";

        string query = BuildQuery(
            ("standard", standardValue),
            ("cif", normalizedCif),
            ("extern", options.ExternalBuyer ? "DA" : null),
            ("autofactura", options.SelfBilling ? "DA" : null),
            ("executare", options.Enforcement ? "DA" : null));

        string url = $"{_apiBase}/{path}?{query}";

        using ByteArrayContent content = new(xml);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        HttpRequestMessage request = new(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await SendAndLogAsync("upload", request, cancellationToken);
        await ThrowForStatusAsync("upload", response, cancellationToken);

        string raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return AnafResponseParser.ParseUpload(raw);
    }

    /// <inheritdoc/>
    public async Task<AnafMessageStatusResult> GetMessageStatusAsync(
        string token, string uploadIndex, CancellationToken cancellationToken = default)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        uploadIndex = Guard.Against.NullOrWhiteSpace(uploadIndex);
        string encodedIndex = Uri.EscapeDataString(uploadIndex);

        string url = $"{_apiBase}/stareMesaj?id_incarcare={encodedIndex}";
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await SendAndLogAsync("status", request, cancellationToken);
        await ThrowForStatusAsync("status", response, cancellationToken);

        string raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return AnafResponseParser.ParseMessageStatus(raw);
    }

    /// <inheritdoc/>
    public async Task<AnafDownloadResult> DownloadMessageAsync(
        string token, string downloadId, CancellationToken cancellationToken = default)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        downloadId = Guard.Against.NullOrWhiteSpace(downloadId);
        string encodedId = Uri.EscapeDataString(downloadId);

        string url = $"{_apiBase}/descarcare?id={encodedId}";
        HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await SendAndLogAsync("download", request, cancellationToken);
        await ThrowForStatusAsync("download", response, cancellationToken);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        if (IsZip(bytes))
        {
            return ParseZipDownload(bytes, downloadId, response.Content.Headers.ContentDisposition);
        }

        string body = System.Text.Encoding.UTF8.GetString(bytes);

        if (AnafDownloadErrorParser.TryGetDownloadWindowExpiredMessage(body, out string? expiredMessage))
        {
            throw new AnafDownloadWindowExpiredException(downloadId, expiredMessage);
        }

        AnafResponseParser.TryParseEroare(body, out string? eroare, out _);
        throw new AnafApiException(
            HttpStatusCode.OK,
            eroare ?? $"ANAF download {downloadId} returned a non-ZIP response.",
            body);
    }

    private static AnafDownloadResult ParseZipDownload(
        byte[] zipBytes, string downloadId, ContentDispositionHeaderValue? contentDisposition)
    {
        byte[]? documentXml = null;
        string? documentFileName = null;
        byte[]? signatureXml = null;
        string? signatureFileName = null;

        try
        {
            using MemoryStream zipStream = new(zipBytes);
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);

            List<ZipArchiveEntry> xmlEntries = archive.Entries
                .Where(e => e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (ZipArchiveEntry entry in xmlEntries)
            {
                using MemoryStream entryStream = new();
                using (Stream stream = entry.Open())
                    stream.CopyTo(entryStream);
                byte[] entryBytes = entryStream.ToArray();

                if (EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(entry.FullName))
                {
                    if (signatureXml == null)
                    {
                        signatureXml = entryBytes;
                        signatureFileName = entry.FullName;
                    }
                }
                else if (documentXml == null)
                {
                    documentXml = entryBytes;
                    documentFileName = entry.FullName;
                }
            }
        }
        catch (InvalidDataException ex)
        {
            throw new AnafApiException(
                HttpStatusCode.OK,
                $"ANAF download {downloadId} returned an invalid ZIP archive.",
                rawResponse: null,
                innerException: ex);
        }

        string fileName = ExtractFileName(contentDisposition) ?? $"{downloadId}.zip";

        return new AnafDownloadResult
        {
            ZipContent = zipBytes,
            FileName = fileName,
            DocumentXml = documentXml,
            DocumentFileName = documentFileName,
            SignatureXml = signatureXml,
            SignatureFileName = signatureFileName
        };
    }

    private static string? ExtractFileName(ContentDispositionHeaderValue? contentDisposition)
    {
        string? raw = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return Path.GetFileName(raw.Trim('"'));
    }

    /// <inheritdoc/>
    public async Task<AnafValidationResult> ValidateWithAnafAsync(
        byte[] xml, AnafDocumentStandard standard, CancellationToken cancellationToken = default)
    {
        if (xml == null || xml.Length == 0)
            throw new ArgumentException("xml must not be null or empty.", nameof(xml));
        if (xml.Length > MaxPublicServiceBytes)
            throw new ArgumentException($"xml exceeds the maximum validation size of {MaxPublicServiceBytes} bytes.", nameof(xml));

        string standardValue = MapPublicServiceStandard(standard);
        string url = $"{_publicBase}/validare/{standardValue}";

        using ByteArrayContent content = new(xml);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        HttpRequestMessage request = new(HttpMethod.Post, url) { Content = content };

        HttpResponseMessage response = await SendAndLogAsync("validate", request, cancellationToken);
        await ThrowForStatusAsync("validate", response, cancellationToken);

        string raw = await response.Content.ReadAsStringAsync(cancellationToken);
        return AnafResponseParser.ParseValidation(raw);
    }

    /// <inheritdoc/>
    public async Task<byte[]> ConvertToPdfAsync(
        byte[] xml, AnafDocumentStandard standard, bool validate = true, CancellationToken cancellationToken = default)
    {
        if (xml == null || xml.Length == 0)
            throw new ArgumentException("xml must not be null or empty.", nameof(xml));
        if (xml.Length > MaxPublicServiceBytes)
            throw new ArgumentException($"xml exceeds the maximum PDF conversion size of {MaxPublicServiceBytes} bytes.", nameof(xml));

        string standardValue = MapPublicServiceStandard(standard);
        string path = validate ? $"transformare/{standardValue}" : $"transformare/{standardValue}/DA";
        string url = $"{_publicBase}/{path}";

        using ByteArrayContent content = new(xml);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        HttpRequestMessage request = new(HttpMethod.Post, url) { Content = content };

        HttpResponseMessage response = await SendAndLogAsync("pdf", request, cancellationToken);
        await ThrowForStatusAsync("pdf", response, cancellationToken);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;

        bool looksLikePdf = mediaType.Contains("application/pdf", StringComparison.OrdinalIgnoreCase)
            || (bytes.Length >= 4 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P' && bytes[2] == (byte)'D' && bytes[3] == (byte)'F');

        if (looksLikePdf)
            return bytes;

        string raw = System.Text.Encoding.UTF8.GetString(bytes);
        AnafValidationResult result = AnafResponseParser.ParseValidation(raw);
        throw new AnafApiException(HttpStatusCode.OK, "ANAF XML-to-PDF conversion failed.", raw, result.Messages);
    }
}
