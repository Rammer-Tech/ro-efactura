using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RoEFactura.Generation;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Tests.Generation.TestData;
using Xunit;
using Xunit.Abstractions;

namespace RoEFactura.Tests.Live;

/// <summary>
/// Opt-in end-to-end proof: each generated case is posted to ANAF's public, stateless validator
/// (<c>validare/FACT1</c>, no authentication, no SPV submission) and must come back valid. Runs only with
/// <c>ANAF_LIVE_VALIDATION=1</c>; synthetic data only. Every row prints one
/// <c>ANAF-LIVE case=... stare=... messages=... trace=...</c> line (visible with
/// <c>dotnet test --logger "console;verbosity=detailed"</c>).
/// </summary>
public class AnafLiveValidationTests
{
    private readonly ITestOutputHelper _output;

    public AnafLiveValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [AnafLiveTheory]
    [MemberData(nameof(EInvoiceTestCases.All), MemberType = typeof(EInvoiceTestCases))]
    public async Task ValidateWithAnaf_GeneratedCase_ReturnsStareOk(string caseName)
    {
        byte[] xml = new EInvoiceXmlGenerator().Generate(EInvoiceTestCases.Get(caseName));

        ServiceCollection services = new();
        services.AddRoEFactura(o => o.PublicServicesBaseUrl = RoEFacturaOptions.DefaultPublicServicesBaseUrl);
        await using ServiceProvider provider = services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        IAnafEInvoiceClient client = scope.ServiceProvider.GetRequiredService<IAnafEInvoiceClient>();

        AnafValidationResult result = await client.ValidateWithAnafAsync(xml, AnafDocumentStandard.Ubl);

        string messages = result.Messages.Count == 0 ? "none" : string.Join(" | ", result.Messages);
        _output.WriteLine(
            $"ANAF-LIVE case={caseName} stare={(result.IsValid ? "ok" : "nok")} messages={messages} trace={result.TraceId}");

        // Keep the public endpoint's request rate low between rows.
        await Task.Delay(1000);

        result.IsValid.Should().BeTrue($"ANAF rejected case {caseName}: {messages}");
    }
}
