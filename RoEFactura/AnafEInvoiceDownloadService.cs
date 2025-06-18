using System.IO.Compression;
using System.Net.Http.Headers;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using RoEFactura.Dtos;

namespace RoEFactura;

public class AnafEInvoiceDownloadService
{
    private readonly HttpClient _httpClient;
    private readonly string _pagedEndpoint;
    private readonly string _nonPagedEndpoint;
    private readonly string _downloadEndpoint;

    public AnafEInvoiceDownloadService(HttpClient httpClient, IHostEnvironment env)
    {
        _httpClient = httpClient;

        // if (env.IsProduction())
        {
            _pagedEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/listaMesajePaginatieFactura";
            _nonPagedEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/listaMesajeFactura";
            _downloadEndpoint = "https://api.anaf.ro/prod/FCTEL/rest/descarcare";
        }
        // else
        // {
        //     _pagedEndpoint = "https://api.anaf.ro/test/FCTEL/rest/listaMesajePaginatieFactura";
        //     _nonPagedEndpoint = "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura";
        //     _downloadEndpoint = "https://api.anaf.ro/test/FCTEL/rest/descarcare";
        // }
    }

    public AnafEInvoiceDownloadService(string pagedEndpoint, string nonPagedEndpoint, string downloadEndpoint)
    {
        _pagedEndpoint = pagedEndpoint;
        _nonPagedEndpoint = nonPagedEndpoint;
        _downloadEndpoint = downloadEndpoint;
    }

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
            HttpResponseMessage response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Error downloading e-invoice. Response: " + response);
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
            HttpResponseMessage response =
                await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Error downloading e-invoice. Response: " + response);
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

        HttpResponseMessage response =
            await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception("Error downloading e-invoice. Response: " + response);
        }

        if (response.Content.Headers.ContentDisposition == null)
        {
            return;
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
}