using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Processing;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Services.Api;

/// <summary>
/// Exact-URL / exact-body contract tests for <see cref="AnafEInvoiceClient"/> against the official
/// ANAF e-Factura endpoints. No real network call is ever made.
/// </summary>
public class AnafEInvoiceClientContractTests
{
    private const string FakeToken = "eyJhbGciOiJSUzI1NiJ9.fake-token-value";

    private static readonly byte[] SampleXmlBytes = Encoding.UTF8.GetBytes(AnafSamples.MinimalInvoiceXml);

    private static (AnafEInvoiceClient Client, RecordingHttpMessageHandler Handler) CreateClient(
        RoEFacturaOptions? options = null,
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? responder = null,
        ILogger<AnafEInvoiceClient>? logger = null)
    {
        options ??= new RoEFacturaOptions();
        responder ??= RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, "{}");

        RecordingHttpMessageHandler handler = new(responder);
        HttpClient httpClient = new(handler);

        RoCiusUblValidator validator = new();
        UblProcessingService processingService = new(validator, NullLogger<UblProcessingService>.Instance);

        AnafEInvoiceClient client = new(
            httpClient,
            Options.Create(options),
            processingService,
            logger ?? NullLogger<AnafEInvoiceClient>.Instance);

        return (client, handler);
    }

    // ── Upload ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UploadAsync_DefaultOptions_PostsToTestUploadUrl()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://api.anaf.ro/test/FCTEL/rest/upload?standard=UBL&cif=12345678");
    }

    [Fact]
    public async Task UploadAsync_ProductionEnvironment_PostsToProdUploadUrl()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            new RoEFacturaOptions { Environment = AnafEnvironment.Production },
            RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://api.anaf.ro/prod/FCTEL/rest/upload?standard=UBL&cif=12345678");
    }

    [Fact]
    public async Task UploadAsync_ApiBaseUrlOverride_UsesOverrideBase()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            new RoEFacturaOptions { ApiBaseUrl = "https://custom.example.test/api" },
            RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://custom.example.test/api/upload?standard=UBL&cif=12345678");
    }

    [Fact]
    public async Task UploadAsync_B2C_PostsToUploadB2cPath()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(
            FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678", IsB2C = true });

        handler.Requests[0].Uri.AbsolutePath.Should().Be("/test/FCTEL/rest/uploadb2c");
    }

    [Fact]
    public async Task UploadAsync_AllFlags_AppendsExternAutofacturaExecutare()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions
        {
            Cif = "12345678",
            ExternalBuyer = true,
            SelfBilling = true,
            Enforcement = true
        });

        handler.Requests[0].Uri.Query.Should().Be(
            "?standard=UBL&cif=12345678&extern=DA&autofactura=DA&executare=DA");
    }

    [Theory]
    [InlineData(AnafDocumentStandard.Ubl, "UBL")]
    [InlineData(AnafDocumentStandard.CreditNote, "CN")]
    [InlineData(AnafDocumentStandard.Cii, "CII")]
    [InlineData(AnafDocumentStandard.Rasp, "RASP")]
    public async Task UploadAsync_Standard_MapsToQueryValue(AnafDocumentStandard standard, string expectedValue)
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(
            FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678", Standard = standard });

        handler.Requests[0].Uri.Query.Should().Contain($"standard={expectedValue}");
    }

    [Fact]
    public async Task UploadAsync_Body_IsRawXmlBytesWithTextPlainAndBearer()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        RecordedRequest request = handler.Requests[0];
        request.ContentMediaType.Should().Be("text/plain");
        request.Body.Should().Equal(SampleXmlBytes);
        request.Authorization.Should().NotBeNull();
        request.Authorization!.Scheme.Should().Be("Bearer");
        request.Authorization.Parameter.Should().Be(FakeToken);
    }

    [Fact]
    public async Task UploadAsync_CifWithRoPrefix_IsNormalizedInQuery()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "RO12345678" });

        handler.Requests[0].Uri.Query.Should().Contain("cif=12345678");
        handler.Requests[0].Uri.Query.Should().NotContain("RO12345678");
    }

    [Fact]
    public async Task UploadAsync_SuccessResponse_ParsesUploadIndexAndDate()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        AnafUploadResult result = await client.UploadAsync(
            FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        result.IsSuccess.Should().BeTrue();
        result.UploadIndex.Should().Be("3828");
        result.ResponseDate.Should().Be(new DateTimeOffset(2021, 8, 5, 11, 40, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public async Task UploadAsync_ErrorResponse_ReturnsFailureWithAllErrors()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadError, "application/xml"));

        AnafUploadResult result = await client.UploadAsync(
            FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().BeEquivalentTo(["Eroare 1", "Eroare 2"]);
    }

    [Fact]
    public async Task UploadAsync_XmlOver10Mb_ThrowsWithoutCallingAnaf()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient();
        byte[] tooLarge = new byte[10 * 1024 * 1024 + 1];

        Func<Task> act = () => client.UploadAsync(
            FakeToken, tooLarge, new AnafUploadOptions { Cif = "12345678" });

        await act.Should().ThrowAsync<ArgumentException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_Http429_ThrowsRateLimitExceptionWithRetryAfter()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(
                HttpStatusCode.TooManyRequests, "{}", "application/json",
                new Dictionary<string, string> { ["Retry-After"] = "30" }));

        Func<Task> act = () => client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        var thrown = await act.Should().ThrowAsync<AnafRateLimitException>();
        thrown.Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task UploadAsync_Http401_ThrowsUnauthorizedAnafApiException()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.Unauthorized, "{}", "application/json"));

        Func<Task> act = () => client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        var thrown = await act.Should().ThrowAsync<AnafApiException>();
        thrown.Which.IsUnauthorized.Should().BeTrue();
    }

    [Fact]
    public async Task UploadAsync_DoesNotLogXmlBodyOrFullToken()
    {
        CapturingLogger<AnafEInvoiceClient> logger = new();
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"),
            logger: logger);

        await client.UploadAsync(FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });

        logger.Messages.Should().NotContain(m => m.Contains("SAMPLE-001"));
        logger.Messages.Should().NotContain(m => m.Contains(FakeToken));
    }

    // ── Status ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMessageStatusAsync_BuildsStareMesajUrl()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.StatusOk, "application/xml"));

        await client.GetMessageStatusAsync(FakeToken, "3828");

        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://api.anaf.ro/test/FCTEL/rest/stareMesaj?id_incarcare=3828");
    }

    [Theory]
    [InlineData(AnafSamples.StatusOk, AnafMessageState.Ok)]
    [InlineData(AnafSamples.StatusNok, AnafMessageState.Nok)]
    [InlineData(AnafSamples.StatusInProcessing, AnafMessageState.InProcessing)]
    [InlineData(AnafSamples.StatusRejected, AnafMessageState.RejectedAtUpload)]
    public async Task GetMessageStatusAsync_MapsStareValue(string xml, AnafMessageState expected)
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, xml, "application/xml"));

        AnafMessageStatusResult result = await client.GetMessageStatusAsync(FakeToken, "3828");

        result.State.Should().Be(expected);
    }

    [Theory]
    [InlineData(AnafSamples.StatusOk, "1234")]
    [InlineData(AnafSamples.StatusNok, "123")]
    public async Task GetMessageStatusAsync_OkAndNok_ReturnDownloadId(string xml, string expectedDownloadId)
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, xml, "application/xml"));

        AnafMessageStatusResult result = await client.GetMessageStatusAsync(FakeToken, "3828");

        result.DownloadId.Should().Be(expectedDownloadId);
    }

    [Fact]
    public async Task GetMessageStatusAsync_ErrorsHeader_ReturnsUnknownWithErrors()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.StatusErrorsOnly, "application/xml"));

        AnafMessageStatusResult result = await client.GetMessageStatusAsync(FakeToken, "18");

        result.State.Should().Be(AnafMessageState.Unknown);
        result.Errors.Should().ContainSingle();
    }

    // ── Lists ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListEInvoicesAsync_DefaultOptions_UsesTestListUrl()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, """{"mesaje":[]}""", "application/json"));

        await client.ListEInvoicesAsync(FakeToken, 30, "12345678", null, CancellationToken.None);

        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://api.anaf.ro/test/FCTEL/rest/listaMesajeFactura?zile=30&cif=12345678");
    }

    [Fact]
    public async Task ListEInvoicesAsync_EroareResponse_ReturnsEmptyList()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ListEroare, "application/json"));

        List<Dtos.EInvoiceAnafResponse> result =
            await client.ListEInvoicesAsync(FakeToken, 30, "12345678", null, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListEInvoicesAsync_RealAnafError_ThrowsAnafApiExceptionWithMessage()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ListEroareNoRight, "application/json"));

        Func<Task> act = () => client.ListEInvoicesAsync(FakeToken, 30, "12345678", null, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<AnafApiException>();
        thrown.Which.Message.Should().Contain("Nu aveti drept in SPV");
        thrown.Which.Errors.Should().ContainSingle(m => m.Contains("Nu aveti drept in SPV"));
    }

    [Fact]
    public async Task ListEInvoicesAsync_DaysAbove60_ThrowsArgumentOutOfRange()
    {
        (AnafEInvoiceClient client, _) = CreateClient();

        Func<Task> act = () => client.ListEInvoicesAsync(FakeToken, 61, "12345678", null, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_BuildsEncodedQuery()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, """{"mesaje":[]}""", "application/json"));

        await client.ListPagedEInvoicesAsync(
            FakeToken, 1646037374000, 1646170574000, "12345678", "E R", 2, CancellationToken.None);

        handler.Requests[0].Uri.AbsoluteUri.Should().Be(
            "https://api.anaf.ro/test/FCTEL/rest/listaMesajePaginatieFactura?" +
            "startTime=1646037374000&endTime=1646170574000&cif=12345678&pagina=2&filtru=E%20R");
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_EroareResponse_ReturnsEmptyItemsAndError()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ListEroare, "application/json"));

        Dtos.EInvoiceAnafPagedListResponse result =
            await client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "12345678", null, 1, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.Error.Should().Be("Nu exista mesaje in ultimele 60 zile");
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_ParsesPaginationFields()
    {
        const string json =
            """{"mesaje":[{"id":"1"}],"numar_total_pagini":29,"numar_total_inregistrari":14130,"index_pagina_curenta":29}""";
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, json, "application/json"));

        Dtos.EInvoiceAnafPagedListResponse result =
            await client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "12345678", null, 29, CancellationToken.None);

        result.PageCount.Should().Be(29);
        result.TotalItemCount.Should().Be(14130);
        result.CurrentPageIndex.Should().Be(29);
        result.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_NullMesaje_ReturnsEmptyItems()
    {
        const string json = """{"numar_total_pagini":1}""";
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, json, "application/json"));

        Dtos.EInvoiceAnafPagedListResponse result =
            await client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "12345678", null, 1, CancellationToken.None);

        result.Items.Should().NotBeNull();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_Http429_ThrowsRateLimitException()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.TooManyRequests, "{}", "application/json"));

        Func<Task> act = () => client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "12345678", null, 1, CancellationToken.None);

        await act.Should().ThrowAsync<AnafRateLimitException>();
    }

    [Fact]
    public async Task ListPagedEInvoicesAsync_CifWithRoPrefix_IsNormalizedInQuery()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, """{"mesaje":[]}""", "application/json"));

        await client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "RO12345678", null, 1, CancellationToken.None);

        handler.Requests[0].Uri.Query.Should().Contain("cif=12345678");
    }

    // ── Download ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task DownloadMessageAsync_BuildsDescarcareUrl()
    {
        byte[] zip = AnafSamples.BuildZip(("factura.xml", AnafSamples.MinimalInvoiceXml));
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, zip, "application/zip"));

        await client.DownloadMessageAsync(FakeToken, "555");

        handler.Requests[0].Uri.AbsoluteUri.Should().Be("https://api.anaf.ro/test/FCTEL/rest/descarcare?id=555");
    }

    [Fact]
    public async Task DownloadMessageAsync_Zip_SplitsDocumentAndSignatureInMemory()
    {
        byte[] zip = AnafSamples.BuildZip(
            ("3828.xml", AnafSamples.MinimalInvoiceXml),
            ("semnatura_3828.xml", "<Signature/>"));
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, zip, "application/zip"));

        AnafDownloadResult result = await client.DownloadMessageAsync(FakeToken, "3828");

        result.DocumentXml.Should().NotBeNull();
        Encoding.UTF8.GetString(result.DocumentXml!).Should().Contain("SAMPLE-001");
        result.DocumentFileName.Should().Be("3828.xml");
        result.SignatureXml.Should().NotBeNull();
        result.SignatureFileName.Should().Be("semnatura_3828.xml");
    }

    [Fact]
    public async Task DownloadMessageAsync_JsonEroare_ThrowsAnafApiException()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.DownloadEroare, "application/json"));

        Func<Task> act = () => client.DownloadMessageAsync(FakeToken, "21");

        await act.Should().ThrowAsync<AnafApiException>();
    }

    [Fact]
    public async Task DownloadMessageAsync_DownloadWindowExpired_ThrowsDownloadWindowExpiredException()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.DownloadWindowExpired, "application/json"));

        Func<Task> act = () => client.DownloadMessageAsync(FakeToken, "7298863146");

        var thrown = await act.Should().ThrowAsync<AnafDownloadWindowExpiredException>();
        thrown.Which.AnafDownloadId.Should().Be("7298863146");
    }

    [Fact]
    public async Task ProcessDownloadedInvoiceAsync_Zip_ReturnsParsedInvoiceWithoutValidation()
    {
        byte[] zip = AnafSamples.BuildZip(("3828.xml", AnafSamples.MinimalInvoiceXml));
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, zip, "application/zip"));

        ProcessingResult<UblSharp.InvoiceType> result =
            await client.ProcessDownloadedInvoiceAsync(FakeToken, "3828", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Data!.ID.Value.Should().Be("SAMPLE-001");
    }

    [Fact]
    public async Task ProcessDownloadedInvoiceAsync_ZipWithOnlySignature_ReturnsFailed()
    {
        byte[] zip = AnafSamples.BuildZip(("semnatura_3828.xml", "<Signature/>"));
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, zip, "application/zip"));

        ProcessingResult<UblSharp.InvoiceType> result =
            await client.ProcessDownloadedInvoiceAsync(FakeToken, "3828", CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadEInvoiceAsync_Obsolete_WritesZipAndExtractsBothFiles()
    {
        byte[] zip = AnafSamples.BuildZip(
            ("3828.xml", AnafSamples.MinimalInvoiceXml),
            ("semnatura_3828.xml", "<Signature/>"));
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(
                HttpStatusCode.OK, zip, "application/zip", contentDispositionFileName: "3828.zip"));

        string tempRoot = Path.Combine(Path.GetTempPath(), "roefactura-tests-" + Guid.NewGuid());
        string zipDir = Path.Combine(tempRoot, "zip");
        string unzipDir = Path.Combine(tempRoot, "unzip");

        try
        {
#pragma warning disable CS0618
            await client.DownloadEInvoiceAsync(FakeToken, zipDir, unzipDir, "3828");
#pragma warning restore CS0618

            File.Exists(Path.Combine(zipDir, "3828.zip")).Should().BeTrue();
            File.Exists(Path.Combine(unzipDir, "3828.xml")).Should().BeTrue();
            File.Exists(Path.Combine(unzipDir, "semnatura_3828.xml")).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task DownloadEInvoiceAsync_EmptyDownloadId_ThrowsBeforeCreatingDirectories()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient();
        string tempRoot = Path.Combine(Path.GetTempPath(), "roefactura-tests-" + Guid.NewGuid());
        string zipDir = Path.Combine(tempRoot, "zip");
        string unzipDir = Path.Combine(tempRoot, "unzip");

        Func<Task> act = () =>
        {
#pragma warning disable CS0618
            return client.DownloadEInvoiceAsync(FakeToken, zipDir, unzipDir, "");
#pragma warning restore CS0618
        };

        await act.Should().ThrowAsync<ArgumentException>();
        Directory.Exists(zipDir).Should().BeFalse();
        Directory.Exists(unzipDir).Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    // ── Validate and PDF ──────────────────────────────────────────────────────

    [Fact]
    public async Task ValidateWithAnafAsync_PostsRawXmlToPublicValidareWithoutAuth()
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateOk, "application/json"));

        await client.ValidateWithAnafAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        RecordedRequest request = handler.Requests[0];
        request.Uri.AbsoluteUri.Should().Be("https://webservicesp.anaf.ro/prod/FCTEL/rest/validare/FACT1");
        request.Authorization.Should().BeNull();
        request.ContentMediaType.Should().Be("text/plain");
    }

    [Theory]
    [InlineData(AnafDocumentStandard.Ubl, "FACT1")]
    [InlineData(AnafDocumentStandard.CreditNote, "FCN")]
    public async Task ValidateWithAnafAsync_Standard_MapsToFact1OrFcn(AnafDocumentStandard standard, string expected)
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateOk, "application/json"));

        await client.ValidateWithAnafAsync(SampleXmlBytes, standard);

        handler.Requests[0].Uri.AbsolutePath.Should().EndWith($"/validare/{expected}");
    }

    [Theory]
    [InlineData(AnafEnvironment.Test)]
    [InlineData(AnafEnvironment.Production)]
    public async Task ValidateWithAnafAsync_AnyEnvironment_AlwaysUsesPublicServicesBaseUrl(AnafEnvironment environment)
    {
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            new RoEFacturaOptions { Environment = environment },
            RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateOk, "application/json"));

        await client.ValidateWithAnafAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        handler.Requests[0].Uri.AbsoluteUri.Should().StartWith("https://webservicesp.anaf.ro/prod/FCTEL/rest/");
    }

    [Fact]
    public async Task ValidateWithAnafAsync_Ok_ReturnsValid()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateOk, "application/json"));

        AnafValidationResult result = await client.ValidateWithAnafAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        result.IsValid.Should().BeTrue();
        result.TraceId.Should().Be("abc-123");
    }

    [Fact]
    public async Task ValidateWithAnafAsync_Nok_ReturnsInvalidWithMessages()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateNok, "application/json"));

        AnafValidationResult result = await client.ValidateWithAnafAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        result.IsValid.Should().BeFalse();
        result.Messages.Should().ContainSingle(m => m.Contains("BR-RO-110"));
    }

    [Fact]
    public async Task ConvertToPdfAsync_Ok_ReturnsPdfBytes()
    {
        byte[] pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake content");
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, pdfBytes, "application/pdf"));

        byte[] result = await client.ConvertToPdfAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        result.Should().Equal(pdfBytes);
        handler.Requests[0].Uri.AbsoluteUri.Should().Be("https://webservicesp.anaf.ro/prod/FCTEL/rest/transformare/FACT1");
    }

    [Fact]
    public async Task ConvertToPdfAsync_WithoutValidation_AppendsDaSegment()
    {
        byte[] pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4 fake content");
        (AnafEInvoiceClient client, RecordingHttpMessageHandler handler) = CreateClient(
            responder: RecordingHttpMessageHandler.ReturningBytes(HttpStatusCode.OK, pdfBytes, "application/pdf"));

        await client.ConvertToPdfAsync(SampleXmlBytes, AnafDocumentStandard.Ubl, validate: false);

        handler.Requests[0].Uri.AbsoluteUri.Should().Be("https://webservicesp.anaf.ro/prod/FCTEL/rest/transformare/FACT1/DA");
    }

    [Fact]
    public async Task ConvertToPdfAsync_Nok_ThrowsAnafApiExceptionWithMessages()
    {
        (AnafEInvoiceClient client, _) = CreateClient(
            responder: RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.ValidateNok, "application/json"));

        Func<Task> act = () => client.ConvertToPdfAsync(SampleXmlBytes, AnafDocumentStandard.Ubl);

        var thrown = await act.Should().ThrowAsync<AnafApiException>();
        thrown.Which.Errors.Should().ContainSingle(m => m.Contains("BR-RO-110"));
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("upload")]
    [InlineData("status")]
    [InlineData("download")]
    [InlineData("validate")]
    [InlineData("pdf")]
    [InlineData("list")]
    [InlineData("list-paged")]
    [InlineData("process-downloaded")]
    [InlineData("process-multiple")]
    public async Task Members_CancellationRequested_ThrowOperationCanceled(string operation)
    {
        (AnafEInvoiceClient client, _) = CreateClient(responder: RecordingHttpMessageHandler.Hanging());
        using CancellationTokenSource cts = new();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        Func<Task> act = operation switch
        {
            "upload" => () => client.UploadAsync(
                FakeToken, SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" }, cts.Token),
            "status" => () => client.GetMessageStatusAsync(FakeToken, "3828", cts.Token),
            "download" => () => client.DownloadMessageAsync(FakeToken, "3828", cts.Token),
            "validate" => () => client.ValidateWithAnafAsync(SampleXmlBytes, AnafDocumentStandard.Ubl, cts.Token),
            "pdf" => () => client.ConvertToPdfAsync(
                SampleXmlBytes, AnafDocumentStandard.Ubl, cancellationToken: cts.Token),
            "list" => () => client.ListEInvoicesAsync(FakeToken, 30, "12345678", null, cts.Token),
            "list-paged" => () => client.ListPagedEInvoicesAsync(FakeToken, 1, 2, "12345678", null, 1, cts.Token),
            "process-downloaded" => () => client.ProcessDownloadedInvoiceAsync(FakeToken, "3828", cts.Token),
            "process-multiple" => () => client.ProcessMultipleInvoicesAsync(FakeToken, ["3828"], cts.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
