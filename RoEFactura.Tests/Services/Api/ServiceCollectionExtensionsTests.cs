using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using Xunit;

namespace RoEFactura.Tests.Services.Api;

/// <summary>
/// DI wiring tests for <see cref="ServiceCollectionExtensions"/>: the TEST environment is the default,
/// Production/override are opt-in, and no <c>IHostEnvironment</c> is required to resolve the client.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    private static readonly byte[] SampleXmlBytes = Encoding.UTF8.GetBytes(AnafSamples.MinimalInvoiceXml);

    private static (IServiceProvider Provider, RecordingHttpMessageHandler Handler) BuildProvider(
        Action<IServiceCollection> configureServices)
    {
        RecordingHttpMessageHandler handler = new(
            RecordingHttpMessageHandler.Returning(HttpStatusCode.OK, AnafSamples.UploadSuccess, "application/xml"));

        ServiceCollection services = new();
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = handler));

        configureServices(services);

        return (services.BuildServiceProvider(validateScopes: true), handler);
    }

    private static Task UploadSampleAsync(IServiceProvider provider)
    {
        using IServiceScope scope = provider.CreateScope();
        IAnafEInvoiceClient client = scope.ServiceProvider.GetRequiredService<IAnafEInvoiceClient>();
        return client.UploadAsync("fake-token", SampleXmlBytes, new AnafUploadOptions { Cif = "12345678" });
    }

    [Fact]
    public async Task AddRoEFactura_WithoutConfiguration_UploadUrlContainsTestPath()
    {
        (IServiceProvider provider, RecordingHttpMessageHandler handler) = BuildProvider(s => s.AddRoEFactura());

        await UploadSampleAsync(provider);

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Uri.AbsoluteUri.Should().Contain("/test/FCTEL/rest/upload");
    }

    [Fact]
    public async Task AddRoEFacturaWithOAuth_ConfigurationWithoutRoEFacturaSection_UsesTestEnvironment()
    {
        Dictionary<string, string?> settings = new()
        {
            ["AnafOAuth:ClientId"] = "client-id",
            ["AnafOAuth:ClientSecret"] = "client-secret",
            ["AnafOAuth:RedirectUri"] = "https://app.example.test/callback"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        (IServiceProvider provider, RecordingHttpMessageHandler handler) =
            BuildProvider(s => s.AddRoEFacturaWithOAuth(configuration));

        await UploadSampleAsync(provider);

        handler.Requests[0].Uri.AbsoluteUri.Should().Contain("/test/FCTEL/rest/upload");
    }

    [Fact]
    public async Task AddRoEFactura_ConfiguredProduction_UsesProdApiBaseUrl()
    {
        Dictionary<string, string?> settings = new()
        {
            ["RoEFactura:Environment"] = "Production"
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        (IServiceProvider provider, RecordingHttpMessageHandler handler) =
            BuildProvider(s => s.AddRoEFactura(configuration));

        await UploadSampleAsync(provider);

        handler.Requests[0].Uri.AbsoluteUri.Should().Contain("/prod/FCTEL/rest/upload");
    }

    [Fact]
    public async Task AddRoEFactura_ActionOverload_AppliesApiBaseUrlOverride()
    {
        (IServiceProvider provider, RecordingHttpMessageHandler handler) = BuildProvider(
            s => s.AddRoEFactura(o => o.ApiBaseUrl = "https://custom.example.test/api"));

        await UploadSampleAsync(provider);

        handler.Requests[0].Uri.AbsoluteUri.Should().StartWith("https://custom.example.test/api/upload");
    }

    [Fact]
    public void AddRoEFactura_DoesNotRequireHostEnvironment()
    {
        (IServiceProvider provider, _) = BuildProvider(s => s.AddRoEFactura());

        using IServiceScope scope = provider.CreateScope();
        Action act = () => scope.ServiceProvider.GetRequiredService<IAnafEInvoiceClient>();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddRoEFactura_InvalidApiBaseUrl_ThrowsInvalidOperationOnResolve()
    {
        (IServiceProvider provider, _) = BuildProvider(
            s => s.AddRoEFactura(o => o.ApiBaseUrl = "not-a-url"));

        using IServiceScope scope = provider.CreateScope();
        Action act = () => scope.ServiceProvider.GetRequiredService<IAnafEInvoiceClient>();

        act.Should().Throw<InvalidOperationException>();
    }
}
