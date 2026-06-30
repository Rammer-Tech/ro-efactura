using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using RoEFactura.Dtos;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Processing;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Services;

public class AnafEInvoiceClientTests
{
    private const string FakeToken = "eyJhbGciOiJSUzI1NiJ9.fake-token";
    private const string FakeCui = "12345678";

    private static (AnafEInvoiceClient client, Mock<HttpMessageHandler> handlerMock)
        CreateClientWithHttp(HttpResponseMessage? defaultResponse = null)
    {
        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(defaultResponse ?? new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{\"mesaje\": [], \"numar_total_inregistrari\": 0}")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        var validator = new RoCiusUblValidator();
        var processingService = new UblProcessingService(
            validator,
            NullLogger<UblProcessingService>.Instance);

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");

        var client = new AnafEInvoiceClient(
            httpClient,
            env.Object,
            processingService,
            NullLogger<AnafEInvoiceClient>.Instance);

        return (client, handlerMock);
    }

    // ── Guard clause tests ────────────────────────────────────────────────────

    [Fact]
    public async Task ListEInvoicesAsync_NullToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.ListEInvoicesAsync(null!, 7, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_EmptyToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListEInvoicesAsync("", 7, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_NullCui_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.ListEInvoicesAsync(FakeToken, 7, null!));
    }

    [Fact]
    public async Task ListEInvoicesAsync_ZeroDays_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListEInvoicesAsync(FakeToken, 0, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_NegativeDays_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ListEInvoicesAsync(FakeToken, -1, FakeCui));
    }

    // ── HTTP request construction tests ──────────────────────────────────────

    [Fact]
    public async Task ListEInvoicesAsync_SendsBearerToken()
    {
        var responseBody = JsonConvert.SerializeObject(new ListEInvoicesAnafResponse
        {
            Items = new List<EInvoiceAnafResponse>()
        });
        var (client, handlerMock) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });

        await client.ListEInvoicesAsync(FakeToken, 7, FakeCui);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Headers.GetValues("Authorization").Single() == $"Bearer {FakeToken}"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ListEInvoicesAsync_IncludesCuiAndDaysInUrl()
    {
        var responseBody = JsonConvert.SerializeObject(new ListEInvoicesAnafResponse
        {
            Items = new List<EInvoiceAnafResponse>()
        });
        var (client, handlerMock) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });

        await client.ListEInvoicesAsync(FakeToken, 30, FakeCui);

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.Query.Contains($"cif={FakeCui}", StringComparison.Ordinal) &&
                req.RequestUri.Query.Contains("zile=30")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ListEInvoicesAsync_WithFilter_IncludesFilterInUrl()
    {
        var responseBody = JsonConvert.SerializeObject(new ListEInvoicesAnafResponse
        {
            Items = new List<EInvoiceAnafResponse>()
        });
        var (client, handlerMock) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });

        await client.ListEInvoicesAsync(FakeToken, 7, FakeCui, filter: "P");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.RequestUri!.Query.Contains("filtru=P")),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ListEInvoicesAsync_HttpError_Throws()
    {
        var (client, _) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<Exception>(() =>
            client.ListEInvoicesAsync(FakeToken, 7, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_DeserializesItems()
    {
        var items = new List<EInvoiceAnafResponse>
        {
            new EInvoiceAnafResponse { Id = "12345" }
        };
        var responseBody = JsonConvert.SerializeObject(new ListEInvoicesAnafResponse { Items = items });
        var (client, _) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });

        var result = await client.ListEInvoicesAsync(FakeToken, 7, FakeCui);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("12345");
    }

    // ── ValidateXmlContentAsync ───────────────────────────────────────────────

    [Fact]
    public async Task ValidateXmlContentAsync_SendsMultipartFormData()
    {
        var (client, handlerMock) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"stare\":\"ok\"}")
            });

        await client.ValidateXmlContentAsync(FakeToken, "<Invoice/>", "invoice.xml");

        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Content is MultipartFormDataContent),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ValidateXmlContentAsync_NullToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.ValidateXmlContentAsync(null!, "<Invoice/>"));
    }

    [Fact]
    public async Task ValidateXmlContentAsync_NullXml_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            client.ValidateXmlContentAsync(FakeToken, null!));
    }

    [Theory]
    [InlineData("{\"eroare\":\"Fisierul nu mai poate fi descarcat pentru ca a trecut perioada de 60 de zile in care este disponibil\"}")]
    [InlineData("Fisierul nu mai poate fi descarcat pentru ca a trecut perioada de 60 de zile")]
    public void AnafDownloadErrorParser_DetectsDownloadWindowExpired(string body)
    {
        AnafDownloadErrorParser.TryGetDownloadWindowExpiredMessage(body, out string? message)
            .Should().BeTrue();
        message.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ProcessDownloadedInvoiceAsync_WhenAnafReturnsExpiredJson_ThrowsDownloadWindowExpired()
    {
        const string downloadId = "7298863146";
        var expiredJson =
            "{\"eroare\":\"Fisierul nu mai poate fi descarcat pentru ca a trecut perioada de 60 de zile in care este disponibil\"}";

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(expiredJson, System.Text.Encoding.UTF8, "text/plain")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var validator = new RoCiusUblValidator();
        var processingService = new UblProcessingService(
            validator,
            NullLogger<UblProcessingService>.Instance);
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");

        var client = new AnafEInvoiceClient(
            httpClient,
            env.Object,
            processingService,
            NullLogger<AnafEInvoiceClient>.Instance);

        var act = () => client.ProcessDownloadedInvoiceAsync(FakeToken, downloadId);

        var ex = await act.Should().ThrowAsync<AnafDownloadWindowExpiredException>();
        ex.Which.AnafDownloadId.Should().Be(downloadId);
    }
}
