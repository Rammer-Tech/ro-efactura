using System.IO.Compression;
using System.Net.Http.Headers;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RoEFactura.Dtos;
using RoEFactura.Extensions;
using RoEFactura.Models;
using RoEFactura.Services.Processing;
using RoEFactura.Utilities;
using UblSharp;

namespace RoEFactura.Services.Api;

internal class AnafEInvoiceClient : IAnafEInvoiceClient
{
    private readonly HttpClient _httpClient;
    private readonly IUblProcessingService _ublProcessingService;
    private readonly ILogger<AnafEInvoiceClient> _logger;
    private readonly string _pagedEndpoint;
    private readonly string _nonPagedEndpoint;
    private readonly string _downloadEndpoint;
    private readonly string _validateEndpoint;
    private readonly string _uploadEndpoint;

    public AnafEInvoiceClient(
        HttpClient httpClient, 
        IHostEnvironment env, 
        IUblProcessingService ublProcessingService,
        ILogger<AnafEInvoiceClient> logger)
    {
        _httpClient = httpClient;
        _ublProcessingService = ublProcessingService;
        _logger = logger;

        // if (env.IsProduction())
        {
            _pagedEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/listaMesajePaginatieFactura";
            _nonPagedEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/listaMesajeFactura";
            _downloadEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/descarcare";
            _validateEndpoint = "https://api.anaf.ro/prod/efactura/validare";
            _uploadEndpoint = "https://api.anaf.ro/prod/efactura/upload";
        }
        // else
        // {
        //     _pagedEndpoint = "https://api.anaf.ro/test/FCTEL/rest/listaMesajePaginatieFactura";
        //     _nonPagedEndpoint = "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura";
        //     _downloadEndpoint = "https://api.anaf.ro/test/FCTEL/rest/descarcare";
        // }
    }

    public AnafEInvoiceClient(string pagedEndpoint, string nonPagedEndpoint, string downloadEndpoint,
        string validateEndpoint, string uploadEndpoint)
    {
        _pagedEndpoint = pagedEndpoint;
        _nonPagedEndpoint = nonPagedEndpoint;
        _downloadEndpoint = downloadEndpoint;
        _validateEndpoint = validateEndpoint;
        _uploadEndpoint = uploadEndpoint;
    }

    /// <summary>
    ///     Send + structured-log an ANAF HTTP exchange. Logs the request URL/method/token-fingerprint
    ///     before sending and the response status/content-type/content-length/content-disposition on
    ///     return. On non-2xx, also logs a body preview (truncated to 1 KB) so callers do not need
    ///     to re-fetch the body for diagnostics.
    /// </summary>
    /// <param name="op">Short operation name used as a log prefix (e.g. "list", "download").</param>
    /// <param name="request">Fully-formed request. Authorization header is logged as a fingerprint only.</param>
    private async Task<HttpResponseMessage> SendAndLogAsync(string op, HttpRequestMessage request)
    {
        string url = request.RequestUri?.ToString() ?? "(no uri)";
        string method = request.Method.Method;
        string tokenFingerprint = "(none)";
        if (request.Headers.Authorization is { Scheme: "Bearer", Parameter: { Length: > 12 } token })
            tokenFingerprint = $"Bearer {token[..8]}…{token[^4..]} (len={token.Length})";

        _logger.LogInformation(
            "ANAF[{Op}] → {Method} {Url} auth={TokenFingerprint}",
            op, method, url, tokenFingerprint);

        HttpResponseMessage response = await _httpClient.SendAsync(request);

        string mediaType = response.Content.Headers.ContentType?.MediaType ?? "(none)";
        long? contentLength = response.Content.Headers.ContentLength;
        bool hasCd = response.Content.Headers.ContentDisposition != null;

        _logger.LogInformation(
            "ANAF[{Op}] ← {Status} {Reason} content-type={MediaType} content-length={Length} content-disposition={HasCd}",
            op, (int)response.StatusCode, response.ReasonPhrase, mediaType, contentLength, hasCd);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            if (body.Length > 1024)
                body = body[..1024] + "…";
            _logger.LogWarning(
                "ANAF[{Op}] non-success body: {Body}",
                op, body);
        }

        return response;
    }

    /// <inheritdoc/>
    public async Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui,
        string filter = null)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        cui = Guard.Against.NullOrWhiteSpace(cui);
        days = Guard.Against.NegativeOrZero(days);

        string requestUrl = $"{_nonPagedEndpoint}?zile={days}&cif={cui}";

        if (!string.IsNullOrWhiteSpace(filter))
        {
            requestUrl += $"&filtru={filter}";
        }

        HttpRequestMessage request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(requestUrl),
            Headers = { { "Authorization", $"Bearer {token}" } }
        };

        try
        {
            HttpResponseMessage response = await SendAndLogAsync("list", request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"ANAF list returned {(int)response.StatusCode} {response.ReasonPhrase} for cif={cui}.");
            }

            string content = await response.Content.ReadAsStringAsync();

            ListEInvoicesAnafResponse result = JsonConvert.DeserializeObject<ListEInvoicesAnafResponse>(content);

            return result.Items;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(string token, long startMilliseconds,
        long endMilliseconds, string cui,
        string filter = null, int page = 1)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        cui = Guard.Against.NullOrWhiteSpace(cui);
        startMilliseconds = Guard.Against.NegativeOrZero(startMilliseconds);
        endMilliseconds = Guard.Against.NegativeOrZero(endMilliseconds);
        page = Guard.Against.NegativeOrZero(page);

        string requestUrl = $"{_pagedEndpoint}?" +
                            $"startTime={startMilliseconds}&" +
                            $"endTime={endMilliseconds}&" +
                            $"cif={cui}&" +
                            $"pagina={page}";

        if (!string.IsNullOrWhiteSpace(filter))
        {
            requestUrl += $"&filtru={filter}";
        }

        HttpRequestMessage request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri(requestUrl),
            Headers = { { "Authorization", $"Bearer {token}" } }
        };

        try
        {
            HttpResponseMessage response = await SendAndLogAsync("list-paged", request);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"ANAF list-paged returned {(int)response.StatusCode} {response.ReasonPhrase} for cif={cui} page={page}.");
            }

            string content = await response.Content.ReadAsStringAsync();

            EInvoiceAnafPagedListResponse
                result = JsonConvert.DeserializeObject<EInvoiceAnafPagedListResponse>(content);

            return result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task DownloadEInvoiceAsync(string token, string zipDestinationPath, string unzipDestinationPath,
        string eInvoiceDownloadId)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        zipDestinationPath = Guard.Against.NullOrWhiteSpace(zipDestinationPath);
        unzipDestinationPath = Guard.Against.NullOrWhiteSpace(unzipDestinationPath);
        eInvoiceDownloadId = Guard.Against.NullOrWhiteSpace(eInvoiceDownloadId);

        if (!Directory.Exists(zipDestinationPath))
        {
            Directory.CreateDirectory(zipDestinationPath);
        }

        if (!Directory.Exists(unzipDestinationPath))
        {
            Directory.CreateDirectory(unzipDestinationPath);
        }

        HttpRequestMessage request = new HttpRequestMessage
        {
            Method = HttpMethod.Get,
            RequestUri = new Uri($"{_downloadEndpoint}?id={eInvoiceDownloadId}"),
            Headers = { { "Authorization", $"Bearer {token}" } }
        };

        HttpResponseMessage response = await SendAndLogAsync("download", request);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"ANAF download returned {(int)response.StatusCode} {response.ReasonPhrase} for id={eInvoiceDownloadId}.");
        }

        if (response.Content.Headers.ContentDisposition == null)
        {
            // ANAF returns 200 OK with a JSON error body (no Content-Disposition) for rate limits,
            // scope rejections, etc. Surface the body instead of silently returning so the caller
            // does not interpret an empty extract directory as "no invoice XML".
            string mediaType = response.Content.Headers.ContentType?.MediaType ?? "(unknown)";
            string bodyPreview = await response.Content.ReadAsStringAsync();
            if (bodyPreview.Length > 500)
                bodyPreview = bodyPreview[..500] + "…";
            throw new InvalidOperationException(
                $"ANAF download {eInvoiceDownloadId} returned {(int)response.StatusCode} with no Content-Disposition " +
                $"(content-type: {mediaType}). Body preview: {bodyPreview}");
        }

        ContentDispositionHeaderValue contentDisposition = response.Content.Headers.ContentDisposition;
        string filename = contentDisposition?.FileNameStar ?? contentDisposition?.FileName;

        string zipFilePath = Path.Combine(zipDestinationPath, filename ?? eInvoiceDownloadId + ".zip");

        await using (Stream stream = await response.Content.ReadAsStreamAsync())
        await using (FileStream fileStream =
                     new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await stream.CopyToAsync(fileStream);
            fileStream.Close();
        }

        Console.WriteLine($"Downloaded e-invoice to {zipFilePath}");

        try
        {
            ZipFile.ExtractToDirectory(zipFilePath, unzipDestinationPath);
            Console.WriteLine($"Extracted e-invoice to {unzipDestinationPath}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> ValidateXmlAsync(string token, string xmlFilePath)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        xmlFilePath = Guard.Against.NullOrWhiteSpace(xmlFilePath);

        if (!File.Exists(xmlFilePath))
        {
            throw new FileNotFoundException("File not found", xmlFilePath);
        }

        using MultipartFormDataContent form = new MultipartFormDataContent();
        using FileStream fileStream = File.OpenRead(xmlFilePath);
        StreamContent fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        form.Add(fileContent, "file", Path.GetFileName(xmlFilePath));

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _validateEndpoint)
        {
            Headers = { { "Authorization", $"Bearer {token}" } },
            Content = form
        };

        HttpResponseMessage response = await SendAndLogAsync("validate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <inheritdoc/>
    public async Task<string> ValidateXmlContentAsync(string token, string xmlContent, string fileName = "invoice.xml")
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        xmlContent = Guard.Against.NullOrWhiteSpace(xmlContent);
        fileName = Guard.Against.NullOrWhiteSpace(fileName);

        using MultipartFormDataContent form = new MultipartFormDataContent();
        byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(xmlContent);
        using MemoryStream memoryStream = new MemoryStream(xmlBytes);
        StreamContent fileContent = new StreamContent(memoryStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        form.Add(fileContent, "file", fileName);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _validateEndpoint)
        {
            Headers = { { "Authorization", $"Bearer {token}" } },
            Content = form
        };

        HttpResponseMessage response = await SendAndLogAsync("validate", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <inheritdoc/>
    public async Task<string> UploadXmlAsync(string token, string xmlFilePath)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        xmlFilePath = Guard.Against.NullOrWhiteSpace(xmlFilePath);

        if (!File.Exists(xmlFilePath))
        {
            throw new FileNotFoundException("File not found", xmlFilePath);
        }

        using MultipartFormDataContent form = new MultipartFormDataContent();
        using FileStream fileStream = File.OpenRead(xmlFilePath);
        StreamContent fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        form.Add(fileContent, "file", Path.GetFileName(xmlFilePath));

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _uploadEndpoint)
        {
            Headers = { { "Authorization", $"Bearer {token}" } },
            Content = form
        };

        HttpResponseMessage response = await SendAndLogAsync("upload", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <inheritdoc/>
    public async Task<string> UploadXmlContentAsync(string token, string xmlContent, string fileName = "invoice.xml")
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        xmlContent = Guard.Against.NullOrWhiteSpace(xmlContent);
        fileName = Guard.Against.NullOrWhiteSpace(fileName);

        using MultipartFormDataContent form = new MultipartFormDataContent();
        byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(xmlContent);
        using MemoryStream memoryStream = new MemoryStream(xmlBytes);
        StreamContent fileContent = new StreamContent(memoryStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
        form.Add(fileContent, "file", fileName);

        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, _uploadEndpoint)
        {
            Headers = { { "Authorization", $"Bearer {token}" } },
            Content = form
        };

        HttpResponseMessage response = await SendAndLogAsync("upload", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <inheritdoc/>
    public async Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        eInvoiceDownloadId = Guard.Against.NullOrWhiteSpace(eInvoiceDownloadId);

        try
        {
            _logger.LogInformation("Processing downloaded invoice with ID: {DownloadId}", eInvoiceDownloadId);

            // Note: Check for existing invoice would be handled at repository level

            // Create temporary directories
            string tempPath = Path.Combine(Path.GetTempPath(), "anaf_invoices", Guid.NewGuid().ToString());
            string zipPath = Path.Combine(tempPath, "downloads");
            string extractPath = Path.Combine(tempPath, "extracted");

            try
            {
                // Download and extract
                await DownloadEInvoiceAsync(token, zipPath, extractPath, eInvoiceDownloadId);

                // Find XML files in extracted directory (skip ANAF signature sidecars, e.g. *semnatura*.xml)
                string[] xmlFiles = Directory.GetFiles(extractPath, "*.xml", SearchOption.AllDirectories);
                string[] invoiceXmlFiles = xmlFiles
                    .Where(f => !EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(f))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                if (!invoiceXmlFiles.Any())
                {
                    _logger.LogWarning(
                        "No invoice XML in downloaded invoice {DownloadId} (missing, or only semnatura XML)",
                        eInvoiceDownloadId);
                    return ProcessingResult<InvoiceType>.Failed(
                        "No invoice XML found in downloaded invoice (only semnatura sidecars or no XML)");
                }

                string xmlFile = invoiceXmlFiles[0];
                string xmlContent = await File.ReadAllTextAsync(xmlFile);

                // Process through UBL pipeline
                byte[] xmlBytes = System.Text.Encoding.UTF8.GetBytes(xmlContent);
                ProcessingResult<InvoiceType> result = await _ublProcessingService.ProcessInvoiceXmlAsync(xmlBytes, eInvoiceDownloadId, skipValidation: true);

                if (result.IsSuccess && result.Data != null)
                {
                    _logger.LogInformation("Successfully processed downloaded invoice {DownloadId} from ANAF", eInvoiceDownloadId);
                }

                return result;
            }
            finally
            {
                // Cleanup temporary directories
                if (Directory.Exists(tempPath))
                {
                    try
                    {
                        Directory.Delete(tempPath, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to cleanup temporary directory: {TempPath}", tempPath);
                    }
                }
            }
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

            // Parse XML content to create an invoice object first
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
    public async Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(
        string token, 
        IEnumerable<string> eInvoiceDownloadIds)
    {
        token = Guard.Against.NullOrWhiteSpace(token);
        List<string> invoiceDownloadIds = eInvoiceDownloadIds.ToList();
        Guard.Against.Null(invoiceDownloadIds);

        List<ProcessingResult<InvoiceType>> results = new List<ProcessingResult<InvoiceType>>();

        _logger.LogInformation("Processing {Count} invoices in batch", invoiceDownloadIds.Count());

        foreach (string downloadId in invoiceDownloadIds)
        {
            try
            {
                ProcessingResult<InvoiceType> result = await ProcessDownloadedInvoiceAsync(token, downloadId);
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
}