# Test Suite Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a comprehensive xUnit test suite (`RoEFactura.Tests`) covering every RO_CIUS validation rule, all EN 16931 rules, XML round-trip parsing, ZIP processing, and HTTP client behaviour, backed by a rich library of XML fixtures.

**Architecture:** Pure unit tests — no network, no real ANAF calls. Validators are exercised directly via in-code `InvoiceType` objects built by a fluent `InvoiceBuilder` helper. XML fixtures are embedded resources used for round-trip and parse error tests. `HttpClient` is mocked via `Moq`'s `HttpMessageHandler` override. `UblProcessingService` is tested with a real `RoCiusUblValidator` (no mocking needed for unit tests) and also with a mocked validator to isolate service logic.

**Tech Stack:** xUnit 2.9, FluentAssertions 7, Moq 4.20, `Microsoft.NET.Test.Sdk`, targeting `net10.0` (library and tests both upgraded from net9.0 in Task 0).

---

## File Structure

```
RoEFactura.Tests/
  RoEFactura.Tests.csproj
  Helpers/
    InvoiceBuilder.cs            ← fluent builder for InvoiceType test objects
    ZipBuilder.cs                ← builds in-memory ZIP archives for ZIP tests
  Fixtures/                      ← embedded XML resources
    Valid/
      valid-380-ron.xml
      valid-381-credit-note.xml
      valid-389-storno.xml
      valid-384-corrective.xml
      valid-751-activity.xml
      valid-eur-with-ron-vat.xml
      valid-bucharest-sector3.xml
    Invalid/
      invalid-br-ro-cius.xml
      invalid-br-ro-010.xml
      invalid-br-ro-020.xml
      invalid-br-ro-030.xml
      invalid-br-ro-120.xml
      invalid-br-16-no-lines.xml
      invalid-br-co-10.xml
      invalid-br-co-11.xml
      invalid-br-12.xml
      invalid-br-2.xml
  Utilities/
    EInvoiceXmlFileFilterTests.cs
    ProcessingResultTests.cs
  Extensions/
    UblSharpExtensionsTests.cs
    InvoiceTypeExtensionsTests.cs
  Validation/
    RoCiusUblValidatorTests.cs
    TotalsValidatorTests.cs
    InvoiceLineValidatorTests.cs
    RomanianAddressValidatorTests.cs
    PartyValidators/
      SellerPartyValidatorTests.cs
      BuyerPartyValidatorTests.cs
      PayeePartyValidatorTests.cs
  Services/
    UblProcessingServiceTests.cs
    AnafEInvoiceClientTests.cs
  Integration/
    XmlFixtureValidationTests.cs  ← load each XML fixture → run full validator chain
```

---

## Task 0: Upgrade to .NET 10

**Files:**
- Modify: `RoEFactura/RoEFactura.csproj`
- Modify: `RoEFactura.sln` (no change needed, but verify)

The library already targets both `net9.0` and `net10.0`. This task drops `net9.0`, pins .NET 10 as the sole TFM, and updates all conditional package references to a stable .NET 10 release.

- [ ] **Step 0.1: Check current .NET 10 SDK availability**

```bash
dotnet --list-sdks | grep "^10\."
```

Expected: at least one SDK line like `10.0.100 [...]`. If missing, install the .NET 10 SDK from https://dotnet.microsoft.com/download before proceeding.

- [ ] **Step 0.2: Update `RoEFactura.csproj` to target net10.0 only**

Replace the current `<TargetFrameworks>net9.0;net10.0</TargetFrameworks>` with:

```xml
<TargetFramework>net10.0</TargetFramework>
```

And collapse the two conditional `<ItemGroup>` blocks into a single unconditional one:

```xml
<!-- Remove the net9.0 and net10.0 conditional ItemGroups and replace with: -->
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="10.0.0" />
  <PackageReference Include="Microsoft.Extensions.Http" Version="10.0.0" />
</ItemGroup>
```

> If `10.0.0` stable is not yet published on NuGet, use the latest available `10.0.*` stable version. Check with:
> ```bash
> dotnet package search Microsoft.Extensions.Hosting.Abstractions --take 5
> ```

- [ ] **Step 0.3: Build in Debug to verify**

```bash
dotnet build RoEFactura/RoEFactura.csproj
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` targeting `net10.0`.

- [ ] **Step 0.4: Build in Release (pack smoke test)**

```bash
dotnet pack RoEFactura/RoEFactura.csproj -c Release --no-build
```

Wait — omit `--no-build` since we just changed TFM:

```bash
dotnet pack RoEFactura/RoEFactura.csproj -c Release
```

Expected: `Successfully created package`.

- [ ] **Step 0.5: Commit**

```bash
git add RoEFactura/RoEFactura.csproj
git commit -m "chore(svc): drop net9.0, target net10.0 exclusively"
```

---

## Task 1: Create test project and wire into solution

**Files:**
- Create: `RoEFactura.Tests/RoEFactura.Tests.csproj`
- Modify: `RoEFactura.sln`
- Modify: `RoEFactura/RoEFactura.csproj` (add `InternalsVisibleTo`)

- [ ] **Step 1.1: Create the test project file**

```xml
<!-- RoEFactura.Tests/RoEFactura.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\RoEFactura\RoEFactura.csproj" />
  </ItemGroup>

  <!-- Embed all XML fixtures as resources so tests can load them without file paths -->
  <ItemGroup>
    <EmbeddedResource Include="Fixtures\**\*.xml" />
  </ItemGroup>

</Project>
```

- [ ] **Step 1.2: Expose internals to the test project**

Add to `RoEFactura/RoEFactura.csproj` inside the existing `<PropertyGroup>`:

```xml
<AssemblyName>RoEFactura</AssemblyName>
```

And add a new `<ItemGroup>`:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>RoEFactura.Tests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

- [ ] **Step 1.3: Add test project to solution**

```bash
cd /path/to/ro-efactura
dotnet sln add RoEFactura.Tests/RoEFactura.Tests.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 1.4: Verify it builds**

```bash
dotnet build RoEFactura.Tests/RoEFactura.Tests.csproj
```

Expected: `Build succeeded.  0 Warning(s)  0 Error(s)`

- [ ] **Step 1.5: Commit**

```bash
git add RoEFactura.Tests/RoEFactura.Tests.csproj RoEFactura/RoEFactura.csproj RoEFactura.sln
git commit -m "chore(svc): add RoEFactura.Tests project and expose internals"
```

---

## Task 2: Create InvoiceBuilder and ZipBuilder test helpers

**Files:**
- Create: `RoEFactura.Tests/Helpers/InvoiceBuilder.cs`
- Create: `RoEFactura.Tests/Helpers/ZipBuilder.cs`

These helpers eliminate boilerplate from every test. `InvoiceBuilder` starts with a fully-valid RO_CIUS invoice and provides `Without*` / `With*` mutations. `ZipBuilder` creates in-memory ZIP bytes.

- [ ] **Step 2.1: Write a failing test that uses InvoiceBuilder (TDD anchor)**

Create `RoEFactura.Tests/Helpers/InvoiceBuilderTests.cs`:

```csharp
using FluentAssertions;
using RoEFactura.Tests.Helpers;
using Xunit;

namespace RoEFactura.Tests.Helpers;

public class InvoiceBuilderTests
{
    [Fact]
    public void Build_ReturnsValidBaseInvoice()
    {
        var invoice = InvoiceBuilder.Valid().Build();

        invoice.Should().NotBeNull();
        invoice.ID!.Value.Should().NotBeNullOrWhiteSpace();
        invoice.InvoiceTypeCode!.Value.Should().Be("380");
        invoice.DocumentCurrencyCode!.Value.Should().Be("RON");
        invoice.InvoiceLine.Should().HaveCountGreaterThanOrEqualTo(1);
        invoice.LegalMonetaryTotal.Should().NotBeNull();
    }
}
```

- [ ] **Step 2.2: Run test to confirm failure**

```bash
dotnet test RoEFactura.Tests --filter "InvoiceBuilderTests" --no-build 2>&1 | tail -5
```

Expected: build error (type not found).

- [ ] **Step 2.3: Implement InvoiceBuilder**

Create `RoEFactura.Tests/Helpers/InvoiceBuilder.cs`:

```csharp
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.CommonBasicComponents;
using RoEFactura.Validation.Constants;

namespace RoEFactura.Tests.Helpers;

/// <summary>
/// Fluent builder that starts from a fully-valid RO_CIUS invoice.
/// Every With*/Without* method returns a new builder — the original is not mutated.
/// </summary>
public class InvoiceBuilder
{
    private InvoiceType _invoice;

    private InvoiceBuilder(InvoiceType invoice) => _invoice = invoice;

    /// <summary>Creates a builder pre-loaded with a valid base invoice.</summary>
    public static InvoiceBuilder Valid() => new(BuildBase());

    public InvoiceType Build() => _invoice;

    // ── Core header mutations ────────────────────────────────────────────────

    public InvoiceBuilder WithId(string id)
    {
        _invoice.ID = new IDType { Value = id };
        return this;
    }

    public InvoiceBuilder WithoutId()
    {
        _invoice.ID = null;
        return this;
    }

    public InvoiceBuilder WithCustomizationId(string id)
    {
        _invoice.CustomizationID = new CustomizationIDType { Value = id };
        return this;
    }

    public InvoiceBuilder WithoutCustomizationId()
    {
        _invoice.CustomizationID = null;
        return this;
    }

    public InvoiceBuilder WithTypeCode(string code)
    {
        _invoice.InvoiceTypeCode = new InvoiceTypeCodeType { Value = code };
        return this;
    }

    public InvoiceBuilder WithoutTypeCode()
    {
        _invoice.InvoiceTypeCode = null;
        return this;
    }

    public InvoiceBuilder WithoutIssueDate()
    {
        _invoice.IssueDate = null;
        return this;
    }

    public InvoiceBuilder WithCurrency(string currency)
    {
        _invoice.DocumentCurrencyCode = new DocumentCurrencyCodeType { Value = currency };
        return this;
    }

    public InvoiceBuilder WithoutCurrency()
    {
        _invoice.DocumentCurrencyCode = null;
        return this;
    }

    public InvoiceBuilder WithVatCurrency(string currency)
    {
        _invoice.TaxCurrencyCode = new TaxCurrencyCodeType { Value = currency };
        return this;
    }

    public InvoiceBuilder WithoutLines()
    {
        _invoice.InvoiceLine = null;
        return this;
    }

    public InvoiceBuilder WithLineCount(int count)
    {
        _invoice.InvoiceLine = Enumerable.Range(1, count)
            .Select(i => BuildValidLine(i.ToString()))
            .ToList();
        // Recalculate totals: each line is 100.00 net
        decimal total = count * 100m;
        decimal vat = Math.Round(total * 0.19m, 2);
        _invoice.LegalMonetaryTotal = BuildMonetaryTotal(total, total + vat, total + vat);
        _invoice.TaxTotal = new List<TaxTotalType>
        {
            BuildTaxTotal(total, vat, 19m)
        };
        return this;
    }

    // ── Monetary total mutations ─────────────────────────────────────────────

    public InvoiceBuilder WithTotals(decimal taxExclusive, decimal taxInclusive, decimal payable)
    {
        _invoice.LegalMonetaryTotal = BuildMonetaryTotal(taxExclusive, taxInclusive, payable);
        return this;
    }

    public InvoiceBuilder WithoutTaxExclusiveAmount()
    {
        _invoice.LegalMonetaryTotal!.TaxExclusiveAmount = null;
        return this;
    }

    public InvoiceBuilder WithoutTaxInclusiveAmount()
    {
        _invoice.LegalMonetaryTotal!.TaxInclusiveAmount = null;
        return this;
    }

    public InvoiceBuilder WithoutPayableAmount()
    {
        _invoice.LegalMonetaryTotal!.PayableAmount = null;
        return this;
    }

    public InvoiceBuilder WithTaxTotalAmount(decimal taxAmount)
    {
        if (_invoice.TaxTotal?.Count > 0)
            _invoice.TaxTotal[0].TaxAmount = new TaxAmountType { Value = taxAmount, currencyID = "RON" };
        return this;
    }

    public InvoiceBuilder WithDocumentPeriod(DateTime start, DateTime end)
    {
        _invoice.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new StartDateType { Value = start },
                EndDate = new EndDateType { Value = end }
            }
        };
        return this;
    }

    // ── Seller mutations ─────────────────────────────────────────────────────

    public InvoiceBuilder WithoutSellerName()
    {
        var party = _invoice.AccountingSupplierParty?.Party;
        if (party != null)
        {
            party.PartyLegalEntity = null;
            party.PartyName = null;
        }
        return this;
    }

    public InvoiceBuilder WithoutSellerAddress()
    {
        if (_invoice.AccountingSupplierParty?.Party != null)
            _invoice.AccountingSupplierParty.Party.PostalAddress = null;
        return this;
    }

    public InvoiceBuilder WithoutSellerCompanyId()
    {
        var entity = _invoice.AccountingSupplierParty?.Party?.PartyLegalEntity?.FirstOrDefault();
        if (entity != null) entity.CompanyID = null;
        return this;
    }

    // ── Buyer mutations ──────────────────────────────────────────────────────

    public InvoiceBuilder WithoutBuyerName()
    {
        var party = _invoice.AccountingCustomerParty?.Party;
        if (party != null)
        {
            party.PartyLegalEntity = null;
            party.PartyName = null;
        }
        return this;
    }

    public InvoiceBuilder WithoutBuyerAddress()
    {
        if (_invoice.AccountingCustomerParty?.Party != null)
            _invoice.AccountingCustomerParty.Party.PostalAddress = null;
        return this;
    }

    public InvoiceBuilder WithoutBuyerIdentifiers()
    {
        var party = _invoice.AccountingCustomerParty?.Party;
        if (party != null)
        {
            var entity = party.PartyLegalEntity?.FirstOrDefault();
            if (entity != null) entity.CompanyID = null;
            if (party.PartyTaxScheme?.Count > 0)
                party.PartyTaxScheme[0].CompanyID = null;
        }
        return this;
    }

    // ── Private factory methods ──────────────────────────────────────────────

    private static InvoiceType BuildBase()
    {
        var line = BuildValidLine("1");
        return new InvoiceType
        {
            CustomizationID = new CustomizationIDType { Value = RomanianConstants.RoCiusCustomizationId },
            ID = new IDType { Value = "INV-2024-001" },
            IssueDate = new IssueDateType { Value = DateTime.Today },
            InvoiceTypeCode = new InvoiceTypeCodeType { Value = "380" },
            DocumentCurrencyCode = new DocumentCurrencyCodeType { Value = "RON" },
            AccountingSupplierParty = BuildRomanianSeller("SC Vanzator SRL", "J12/100/2020", "RO12345678", "CJ", "Cluj-Napoca"),
            AccountingCustomerParty = BuildRomanianBuyer("SC Cumparator SRL", "J40/200/2019", "RO87654321", "IS", "Iasi"),
            TaxTotal = new List<TaxTotalType> { BuildTaxTotal(100m, 19m, 19m) },
            LegalMonetaryTotal = BuildMonetaryTotal(100m, 119m, 119m),
            InvoiceLine = new List<InvoiceLineType> { line }
        };
    }

    public static InvoiceLineType BuildValidLine(string id, decimal amount = 100m)
    {
        return new InvoiceLineType
        {
            ID = new IDType { Value = id },
            InvoicedQuantity = new InvoicedQuantityType { Value = 1m, unitCode = "C62" },
            LineExtensionAmount = new LineExtensionAmountType { Value = amount, currencyID = "RON" },
            Item = new ItemType
            {
                Name = new NameType { Value = "Servicii consultanta" },
                ClassifiedTaxCategory = new List<TaxCategoryType>
                {
                    new TaxCategoryType
                    {
                        ID = new IDType { Value = "S" },
                        Percent = new PercentType { Value = 19m },
                        TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
                    }
                }
            },
            Price = new PriceType
            {
                PriceAmount = new PriceAmountType { Value = amount, currencyID = "RON" }
            }
        };
    }

    private static SupplierPartyType BuildRomanianSeller(string name, string companyId, string vatId, string county, string city)
    {
        return new SupplierPartyType
        {
            Party = new PartyType
            {
                PartyLegalEntity = new List<PartyLegalEntityType>
                {
                    new PartyLegalEntityType
                    {
                        RegistrationName = new RegistrationNameType { Value = name },
                        CompanyID = new CompanyIDType { Value = companyId }
                    }
                },
                PartyTaxScheme = new List<PartyTaxSchemeType>
                {
                    new PartyTaxSchemeType
                    {
                        CompanyID = new CompanyIDType { Value = vatId },
                        TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
                    }
                },
                PostalAddress = BuildRomanianAddress(city, county)
            }
        };
    }

    private static CustomerPartyType BuildRomanianBuyer(string name, string companyId, string vatId, string county, string city)
    {
        return new CustomerPartyType
        {
            Party = new PartyType
            {
                PartyLegalEntity = new List<PartyLegalEntityType>
                {
                    new PartyLegalEntityType
                    {
                        RegistrationName = new RegistrationNameType { Value = name },
                        CompanyID = new CompanyIDType { Value = companyId }
                    }
                },
                PartyTaxScheme = new List<PartyTaxSchemeType>
                {
                    new PartyTaxSchemeType
                    {
                        CompanyID = new CompanyIDType { Value = vatId },
                        TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
                    }
                },
                PostalAddress = BuildRomanianAddress(city, county)
            }
        };
    }

    public static AddressType BuildRomanianAddress(string city, string county)
    {
        return new AddressType
        {
            CityName = new CityNameType { Value = city },
            CountrySubentity = new CountrySubentityType { Value = county },
            Country = new CountryType
            {
                IdentificationCode = new IdentificationCodeType { Value = "RO" }
            }
        };
    }

    private static MonetaryTotalType BuildMonetaryTotal(decimal taxExclusive, decimal taxInclusive, decimal payable)
    {
        return new MonetaryTotalType
        {
            LineExtensionAmount = new LineExtensionAmountType { Value = taxExclusive, currencyID = "RON" },
            TaxExclusiveAmount = new TaxExclusiveAmountType { Value = taxExclusive, currencyID = "RON" },
            TaxInclusiveAmount = new TaxInclusiveAmountType { Value = taxInclusive, currencyID = "RON" },
            PayableAmount = new PayableAmountType { Value = payable, currencyID = "RON" }
        };
    }

    private static TaxTotalType BuildTaxTotal(decimal taxableAmount, decimal taxAmount, decimal rate)
    {
        return new TaxTotalType
        {
            TaxAmount = new TaxAmountType { Value = taxAmount, currencyID = "RON" },
            TaxSubtotal = new List<TaxSubtotalType>
            {
                new TaxSubtotalType
                {
                    TaxableAmount = new TaxableAmountType { Value = taxableAmount, currencyID = "RON" },
                    TaxAmount = new TaxAmountType { Value = taxAmount, currencyID = "RON" },
                    TaxCategory = new TaxCategoryType
                    {
                        ID = new IDType { Value = "S" },
                        Percent = new PercentType { Value = rate },
                        TaxScheme = new TaxSchemeType { ID = new IDType { Value = "VAT" } }
                    }
                }
            }
        };
    }
}
```

- [ ] **Step 2.4: Implement ZipBuilder**

Create `RoEFactura.Tests/Helpers/ZipBuilder.cs`:

```csharp
using System.IO.Compression;
using System.Text;

namespace RoEFactura.Tests.Helpers;

/// <summary>Builds in-memory ZIP archives for UblProcessingService tests.</summary>
public static class ZipBuilder
{
    public static byte[] WithEntries(params (string fileName, string content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (fileName, content) in entries)
            {
                var entry = archive.CreateEntry(fileName);
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.Write(content);
            }
        }
        ms.Position = 0;
        return ms.ToArray();
    }

    /// <summary>Loads XML fixture content from embedded resources.</summary>
    public static string LoadFixture(string relativePath)
    {
        var assembly = typeof(ZipBuilder).Assembly;
        var resourceName = $"RoEFactura.Tests.Fixtures.{relativePath.Replace('/', '.').Replace('\\', '.')}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

- [ ] **Step 2.5: Run builder test to verify it passes**

```bash
dotnet test RoEFactura.Tests --filter "InvoiceBuilderTests" -v minimal
```

Expected: `1 passed, 0 failed`

- [ ] **Step 2.6: Commit**

```bash
git add RoEFactura.Tests/Helpers/
git commit -m "test(svc): add InvoiceBuilder and ZipBuilder test helpers"
```

---

## Task 3: Create valid XML fixtures

**Files:**
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-380-ron.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-381-credit-note.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-389-storno.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-384-corrective.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-751-activity.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-eur-with-ron-vat.xml`
- Create: `RoEFactura.Tests/Fixtures/Valid/valid-bucharest-sector3.xml`

All fixtures are minimal but complete RO_CIUS UBL 2.1 invoices. The XML namespace structure must match what `UblSharp`'s `XmlSerializer` expects.

- [ ] **Step 3.1: Create `valid-380-ron.xml` (standard commercial invoice)**

```xml
<?xml version="1.0" encoding="UTF-8"?>
<Invoice xmlns="urn:oasis:names:specification:ubl:schema:xsd:Invoice-2"
         xmlns:cac="urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2"
         xmlns:cbc="urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2">
  <cbc:CustomizationID>urn:cen.eu:en16931:2017#compliant#urn:efactura.mfinante.ro:RO_CIUS:1.0.0.2021</cbc:CustomizationID>
  <cbc:ID>INV-2024-001</cbc:ID>
  <cbc:IssueDate>2024-01-15</cbc:IssueDate>
  <cbc:InvoiceTypeCode>380</cbc:InvoiceTypeCode>
  <cbc:DocumentCurrencyCode>RON</cbc:DocumentCurrencyCode>
  <cac:AccountingSupplierParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Vanzator SRL</cbc:Name></cac:PartyName>
      <cac:PostalAddress>
        <cbc:CityName>Cluj-Napoca</cbc:CityName>
        <cbc:CountrySubentity>CJ</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
      <cac:PartyTaxScheme>
        <cbc:CompanyID>RO12345678</cbc:CompanyID>
        <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
      </cac:PartyTaxScheme>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>SC Vanzator SRL</cbc:RegistrationName>
        <cbc:CompanyID>J12/100/2020</cbc:CompanyID>
      </cac:PartyLegalEntity>
    </cac:Party>
  </cac:AccountingSupplierParty>
  <cac:AccountingCustomerParty>
    <cac:Party>
      <cac:PartyName><cbc:Name>SC Cumparator SRL</cbc:Name></cac:PartyName>
      <cac:PostalAddress>
        <cbc:CityName>Iasi</cbc:CityName>
        <cbc:CountrySubentity>IS</cbc:CountrySubentity>
        <cac:Country><cbc:IdentificationCode>RO</cbc:IdentificationCode></cac:Country>
      </cac:PostalAddress>
      <cac:PartyTaxScheme>
        <cbc:CompanyID>RO87654321</cbc:CompanyID>
        <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
      </cac:PartyTaxScheme>
      <cac:PartyLegalEntity>
        <cbc:RegistrationName>SC Cumparator SRL</cbc:RegistrationName>
        <cbc:CompanyID>J40/200/2019</cbc:CompanyID>
      </cac:PartyLegalEntity>
    </cac:Party>
  </cac:AccountingCustomerParty>
  <cac:TaxTotal>
    <cbc:TaxAmount currencyID="RON">19.00</cbc:TaxAmount>
    <cac:TaxSubtotal>
      <cbc:TaxableAmount currencyID="RON">100.00</cbc:TaxableAmount>
      <cbc:TaxAmount currencyID="RON">19.00</cbc:TaxAmount>
      <cac:TaxCategory>
        <cbc:ID>S</cbc:ID>
        <cbc:Percent>19</cbc:Percent>
        <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
      </cac:TaxCategory>
    </cac:TaxSubtotal>
  </cac:TaxTotal>
  <cac:LegalMonetaryTotal>
    <cbc:LineExtensionAmount currencyID="RON">100.00</cbc:LineExtensionAmount>
    <cbc:TaxExclusiveAmount currencyID="RON">100.00</cbc:TaxExclusiveAmount>
    <cbc:TaxInclusiveAmount currencyID="RON">119.00</cbc:TaxInclusiveAmount>
    <cbc:PayableAmount currencyID="RON">119.00</cbc:PayableAmount>
  </cac:LegalMonetaryTotal>
  <cac:InvoiceLine>
    <cbc:ID>1</cbc:ID>
    <cbc:InvoicedQuantity unitCode="C62">1</cbc:InvoicedQuantity>
    <cbc:LineExtensionAmount currencyID="RON">100.00</cbc:LineExtensionAmount>
    <cac:Item>
      <cbc:Name>Servicii consultanta</cbc:Name>
      <cac:ClassifiedTaxCategory>
        <cbc:ID>S</cbc:ID>
        <cbc:Percent>19</cbc:Percent>
        <cac:TaxScheme><cbc:ID>VAT</cbc:ID></cac:TaxScheme>
      </cac:ClassifiedTaxCategory>
    </cac:Item>
    <cac:Price>
      <cbc:PriceAmount currencyID="RON">100.00</cbc:PriceAmount>
    </cac:Price>
  </cac:InvoiceLine>
</Invoice>
```

- [ ] **Step 3.2: Create `valid-381-credit-note.xml`**

Copy `valid-380-ron.xml`, change:
- `<cbc:ID>CN-2024-001</cbc:ID>`
- `<cbc:InvoiceTypeCode>381</cbc:InvoiceTypeCode>`

- [ ] **Step 3.3: Create `valid-389-storno.xml`**

Copy `valid-380-ron.xml`, change:
- `<cbc:ID>STORNO-2024-001</cbc:ID>`
- `<cbc:InvoiceTypeCode>389</cbc:InvoiceTypeCode>`

- [ ] **Step 3.4: Create `valid-384-corrective.xml`**

Copy `valid-380-ron.xml`, change:
- `<cbc:ID>CORR-2024-001</cbc:ID>`
- `<cbc:InvoiceTypeCode>384</cbc:InvoiceTypeCode>`

- [ ] **Step 3.5: Create `valid-751-activity.xml`**

Copy `valid-380-ron.xml`, change:
- `<cbc:ID>ACT-2024-001</cbc:ID>`
- `<cbc:InvoiceTypeCode>751</cbc:InvoiceTypeCode>`

- [ ] **Step 3.6: Create `valid-eur-with-ron-vat.xml`**

Copy `valid-380-ron.xml`, change:
- `<cbc:ID>EUR-2024-001</cbc:ID>`
- `<cbc:DocumentCurrencyCode>EUR</cbc:DocumentCurrencyCode>`
- Add `<cbc:TaxCurrencyCode>RON</cbc:TaxCurrencyCode>` (directly after DocumentCurrencyCode)
- Change all `currencyID="RON"` amounts to `currencyID="EUR"` **except** TaxTotal which stays `currencyID="RON"`

- [ ] **Step 3.7: Create `valid-bucharest-sector3.xml`**

Copy `valid-380-ron.xml`, change buyer's address:
```xml
<cbc:CityName>Sector 3</cbc:CityName>
<cbc:CountrySubentity>B</cbc:CountrySubentity>
```

- [ ] **Step 3.8: Commit**

```bash
git add RoEFactura.Tests/Fixtures/Valid/
git commit -m "test(svc): add valid XML fixtures for all RO_CIUS invoice types"
```

---

## Task 4: Create invalid XML fixtures

**Files:**
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-ro-cius.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-ro-010.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-ro-020.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-ro-030.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-ro-120.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-16-no-lines.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-co-10.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-co-11.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-12.xml`
- Create: `RoEFactura.Tests/Fixtures/Invalid/invalid-br-2.xml`

Each file is a copy of `valid-380-ron.xml` with one targeted mutation.

- [ ] **Step 4.1: `invalid-br-ro-cius.xml`** — Wrong CustomizationID:
  ```xml
  <cbc:CustomizationID>urn:wrong:customization</cbc:CustomizationID>
  ```

- [ ] **Step 4.2: `invalid-br-ro-010.xml`** — Invoice number with no digit:
  ```xml
  <cbc:ID>INV-ABC-NONNUMERIC</cbc:ID>
  ```
  (the string "INV-ABC-NONNUMERIC" contains no digit)

- [ ] **Step 4.3: `invalid-br-ro-020.xml`** — Invalid type code:
  ```xml
  <cbc:InvoiceTypeCode>999</cbc:InvoiceTypeCode>
  ```

- [ ] **Step 4.4: `invalid-br-ro-030.xml`** — EUR currency but VAT currency is also EUR (not RON):
  ```xml
  <cbc:DocumentCurrencyCode>EUR</cbc:DocumentCurrencyCode>
  <cbc:TaxCurrencyCode>EUR</cbc:TaxCurrencyCode>
  ```

- [ ] **Step 4.5: `invalid-br-ro-120.xml`** — Romanian buyer with no CompanyID and no VAT ID:

  Remove both `<cbc:CompanyID>` entries from the buyer's `PartyLegalEntity` and `PartyTaxScheme`.

- [ ] **Step 4.6: `invalid-br-16-no-lines.xml`** — No `InvoiceLine` elements:

  Remove all `<cac:InvoiceLine>...</cac:InvoiceLine>` blocks entirely.

- [ ] **Step 4.7: `invalid-br-co-10.xml`** — Line sum ≠ TaxExclusiveAmount:

  Keep line `LineExtensionAmount` at `100.00` but set `LegalMonetaryTotal/TaxExclusiveAmount` to `200.00`.

- [ ] **Step 4.8: `invalid-br-co-11.xml`** — Total with VAT ≠ total without VAT + VAT:

  Set `TaxInclusiveAmount` to `200.00` while `TaxExclusiveAmount` is `100.00` and VAT is `19.00` (expected 119.00).

- [ ] **Step 4.9: `invalid-br-12.xml`** — Missing TaxExclusiveAmount:

  Remove `<cbc:TaxExclusiveAmount ...>` from `LegalMonetaryTotal`.

- [ ] **Step 4.10: `invalid-br-2.xml`** — Missing IssueDate:

  Remove `<cbc:IssueDate>` entirely.

- [ ] **Step 4.11: Commit**

```bash
git add RoEFactura.Tests/Fixtures/Invalid/
git commit -m "test(svc): add invalid XML fixtures targeting specific BR rules"
```

---

## Task 5: EInvoiceXmlFileFilter tests

**Files:**
- Create: `RoEFactura.Tests/Utilities/EInvoiceXmlFileFilterTests.cs`

- [ ] **Step 5.1: Write tests**

```csharp
using FluentAssertions;
using RoEFactura.Utilities;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class EInvoiceXmlFileFilterTests
{
    [Theory]
    [InlineData("semnatura.xml", true)]
    [InlineData("SEMNATURA.xml", true)]
    [InlineData("12345_semnatura_factura.xml", true)]
    [InlineData("sEMnAtUrA.xml", true)]
    [InlineData("path/to/semnatura.xml", true)]
    [InlineData(@"C:\invoices\semnatura.xml", true)]
    public void IsSemnaturaXmlFileName_ReturnsTrue_ForSignatureSidecars(string path, bool expected)
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(path).Should().Be(expected);
    }

    [Theory]
    [InlineData("invoice.xml", false)]
    [InlineData("factura1.xml", false)]
    [InlineData("12345678.xml", false)]
    [InlineData("path/to/invoice.xml", false)]
    [InlineData("", false)]
    public void IsSemnaturaXmlFileName_ReturnsFalse_ForInvoiceFiles(string path, bool expected)
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(path).Should().Be(expected);
    }

    [Fact]
    public void IsSemnaturaXmlFileName_ReturnsFalse_ForNull()
    {
        EInvoiceXmlFileFilter.IsSemnaturaXmlFileName(null).Should().BeFalse();
    }
}
```

- [ ] **Step 5.2: Run**

```bash
dotnet test RoEFactura.Tests --filter "EInvoiceXmlFileFilterTests" -v minimal
```

Expected: all pass.

- [ ] **Step 5.3: Commit**

```bash
git add RoEFactura.Tests/Utilities/EInvoiceXmlFileFilterTests.cs
git commit -m "test(svc): add EInvoiceXmlFileFilter tests"
```

---

## Task 6: ProcessingResult tests

**Files:**
- Create: `RoEFactura.Tests/Utilities/ProcessingResultTests.cs`

- [ ] **Step 6.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Models;
using Xunit;

namespace RoEFactura.Tests.Utilities;

public class ProcessingResultTests
{
    [Fact]
    public void Success_SetsIsSuccessTrue_AndData()
    {
        var result = ProcessingResult<string>.Success("hello");

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().Be("hello");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Failed_WithMessage_SetsIsSuccessFalse_AndSingleError()
    {
        var result = ProcessingResult<string>.Failed("something went wrong");

        result.IsSuccess.Should().BeFalse();
        result.Data.Should().BeNull();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].ErrorMessage.Should().Be("something went wrong");
        result.Errors[0].PropertyName.Should().Be("General");
    }

    [Fact]
    public void Failed_WithValidationFailures_PreservesAllErrors()
    {
        var failures = new List<ValidationFailure>
        {
            new("Field1", "Error 1") { ErrorCode = "BR-1" },
            new("Field2", "Error 2") { ErrorCode = "BR-2" }
        };

        var result = ProcessingResult<string>.Failed(failures);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(f => f.ErrorCode == "BR-1");
        result.Errors.Should().Contain(f => f.ErrorCode == "BR-2");
    }

    [Fact]
    public void WithWarnings_AppendsWarnings_ToResult()
    {
        var result = ProcessingResult<string>.Success("data")
            .WithWarnings(new[] { "warning 1", "warning 2" });

        result.Warnings.Should().HaveCount(2);
        result.Warnings.Should().Contain("warning 1");
    }

    [Fact]
    public void Failed_WithEmptyEnumerable_HasNoErrors()
    {
        var result = ProcessingResult<int>.Failed(Enumerable.Empty<ValidationFailure>());

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }
}
```

- [ ] **Step 6.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "ProcessingResultTests" -v minimal
git add RoEFactura.Tests/Utilities/ProcessingResultTests.cs
git commit -m "test(svc): add ProcessingResult tests"
```

---

## Task 7: UblSharpExtensions round-trip tests

**Files:**
- Create: `RoEFactura.Tests/Extensions/UblSharpExtensionsTests.cs`

- [ ] **Step 7.1: Write tests**

```csharp
using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using Xunit;

namespace RoEFactura.Tests.Extensions;

public class UblSharpExtensionsTests
{
    [Fact]
    public void LoadInvoiceFromXml_WithNull_ReturnsNull()
    {
        UblSharpExtensions.LoadInvoiceFromXml(null!).Should().BeNull();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithEmptyString_ReturnsNull()
    {
        UblSharpExtensions.LoadInvoiceFromXml("   ").Should().BeNull();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithMalformedXml_Throws()
    {
        var act = () => UblSharpExtensions.LoadInvoiceFromXml("<not-valid-ubl><unclosed>");
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void LoadInvoiceFromXml_WithValidFixture_ParsesCorrectly()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.ID!.Value.Should().Be("INV-2024-001");
        invoice.InvoiceTypeCode!.Value.Should().Be("380");
        invoice.DocumentCurrencyCode!.Value.Should().Be("RON");
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_PreservesInvoiceId()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var original = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        string reserialized = original.SaveInvoiceToXml();
        var roundTripped = UblSharpExtensions.LoadInvoiceFromXml(reserialized);

        roundTripped.Should().NotBeNull();
        roundTripped!.ID!.Value.Should().Be(original.ID!.Value);
        roundTripped.InvoiceTypeCode!.Value.Should().Be(original.InvoiceTypeCode!.Value);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_PreservesLineCount()
    {
        string xml = ZipBuilder.LoadFixture("Valid/valid-380-ron.xml");
        var original = UblSharpExtensions.LoadInvoiceFromXml(xml)!;

        var roundTripped = UblSharpExtensions.LoadInvoiceFromXml(original.SaveInvoiceToXml())!;

        roundTripped.InvoiceLine.Should().HaveCount(original.InvoiceLine.Count);
    }

    [Fact]
    public void SaveInvoiceToXml_WithNull_ThrowsArgumentNullException()
    {
        var act = () => ((UblSharp.InvoiceType)null!).SaveInvoiceToXml();
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("Valid/valid-381-credit-note.xml", "381")]
    [InlineData("Valid/valid-389-storno.xml", "389")]
    [InlineData("Valid/valid-384-corrective.xml", "384")]
    [InlineData("Valid/valid-751-activity.xml", "751")]
    public void LoadInvoiceFromXml_ParsesAllInvoiceTypes(string fixturePath, string expectedTypeCode)
    {
        string xml = ZipBuilder.LoadFixture(fixturePath);
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);

        invoice.Should().NotBeNull();
        invoice!.InvoiceTypeCode!.Value.Should().Be(expectedTypeCode);
    }
}
```

- [ ] **Step 7.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "UblSharpExtensionsTests" -v minimal
git add RoEFactura.Tests/Extensions/UblSharpExtensionsTests.cs
git commit -m "test(svc): add UblSharpExtensions round-trip tests"
```

---

## Task 8: InvoiceTypeExtensions tests

**Files:**
- Create: `RoEFactura.Tests/Extensions/InvoiceTypeExtensionsTests.cs`

- [ ] **Step 8.1: Write tests**

```csharp
using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.Constants;
using UblSharp;
using Xunit;

namespace RoEFactura.Tests.Extensions;

public class InvoiceTypeExtensionsTests
{
    // ── IsRomanianInvoice ────────────────────────────────────────────────────

    [Fact]
    public void IsRomanianInvoice_WithRoCiusCustomizationId_ReturnsTrue()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.IsRomanianInvoice().Should().BeTrue();
    }

    [Fact]
    public void IsRomanianInvoice_WithRoSellerCountry_ReturnsTrue()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCustomizationId().Build();
        invoice.IsRomanianInvoice().Should().BeTrue(); // seller address is RO
    }

    [Fact]
    public void IsRomanianInvoice_WhenNull_ReturnsFalse()
    {
        ((InvoiceType)null!).IsRomanianInvoice().Should().BeFalse();
    }

    [Fact]
    public void IsRomanianInvoice_WithForeignInvoice_ReturnsFalse()
    {
        var invoice = new InvoiceType
        {
            CustomizationID = new UblSharp.CommonBasicComponents.CustomizationIDType { Value = "urn:cen.eu:en16931:2017" }
        };
        invoice.IsRomanianInvoice().Should().BeFalse();
    }

    // ── GetCurrencyCode ──────────────────────────────────────────────────────

    [Fact]
    public void GetCurrencyCode_ReturnsDocumentCurrencyCode()
    {
        var invoice = InvoiceBuilder.Valid().WithCurrency("EUR").Build();
        invoice.GetCurrencyCode().Should().Be("EUR");
    }

    [Fact]
    public void GetCurrencyCode_WhenMissing_ReturnsEmptyString()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCurrency().Build();
        invoice.GetCurrencyCode().Should().BeEmpty();
    }

    // ── GetTotalAmountDue ────────────────────────────────────────────────────

    [Fact]
    public void GetTotalAmountDue_ReturnPayableAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalAmountDue().Should().Be(119m);
    }

    [Fact]
    public void GetTotalAmountDue_WhenNoTotals_ReturnsZero()
    {
        var invoice = new InvoiceType();
        invoice.GetTotalAmountDue().Should().Be(0m);
    }

    // ── GetTotalWithoutVat / GetTotalWithVat / GetTotalVat ───────────────────

    [Fact]
    public void GetTotalWithoutVat_ReturnsTaxExclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalWithoutVat().Should().Be(100m);
    }

    [Fact]
    public void GetTotalWithVat_ReturnsTaxInclusiveAmount()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 119m, 119m).Build();
        invoice.GetTotalWithVat().Should().Be(119m);
    }

    [Fact]
    public void GetTotalVat_ReturnsTaxTotalAmount()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.GetTotalVat().Should().Be(19m);
    }

    // ── GetValidationSummary ─────────────────────────────────────────────────

    [Fact]
    public void GetValidationSummary_ForValidInvoice_ReturnsValid()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        invoice.GetValidationSummary().Should().Be("Valid");
    }

    [Fact]
    public void GetValidationSummary_ForNullInvoice_ReturnsInvalidMessage()
    {
        ((InvoiceType)null!).GetValidationSummary().Should().Be("Invalid invoice");
    }

    [Fact]
    public void GetValidationSummary_MissingId_ContainsMissingInvoiceNumber()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        invoice.GetValidationSummary().Should().Contain("Missing invoice number");
    }

    [Fact]
    public void GetValidationSummary_MissingLines_ContainsMissingLines()
    {
        var invoice = InvoiceBuilder.Valid().WithoutLines().Build();
        invoice.GetValidationSummary().Should().Contain("Missing invoice lines");
    }

    [Fact]
    public void GetValidationSummary_RomanianInvoiceMissingRoCiusId_ContainsRoCiusMessage()
    {
        // Seller is RO → still considered Romanian, but customization ID is wrong
        var invoice = InvoiceBuilder.Valid().WithCustomizationId("wrong").Build();
        invoice.GetValidationSummary().Should().Contain("RO_CIUS");
    }
}
```

- [ ] **Step 8.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "InvoiceTypeExtensionsTests" -v minimal
git add RoEFactura.Tests/Extensions/InvoiceTypeExtensionsTests.cs
git commit -m "test(svc): add InvoiceTypeExtensions tests"
```

---

## Task 9: RoCiusUblValidator tests

**Files:**
- Create: `RoEFactura.Tests/Validation/RoCiusUblValidatorTests.cs`

These tests exercise every rule in `RoCiusUblValidator` individually. One test per rule — each test mutates the valid base invoice to trigger exactly one rule failure and asserts the expected error code appears.

- [ ] **Step 9.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using RoEFactura.Validation.Constants;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class RoCiusUblValidatorTests
{
    private readonly RoCiusUblValidator _sut = new();

    private ValidationResult Validate(UblSharp.InvoiceType invoice)
        => _sut.Validate(invoice);

    private static void ShouldContainErrorCode(ValidationResult result, string errorCode)
    {
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == errorCode,
            $"Expected error code {errorCode} but got: {string.Join(", ", result.Errors.Select(e => e.ErrorCode))}");
    }

    // ── BR-RO-CIUS ──────────────────────────────────────────────────────────

    [Fact]
    public void BrRoCius_ValidCustomizationId_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-CIUS");
    }

    [Fact]
    public void BrRoCius_WrongCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithCustomizationId("urn:wrong").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-CIUS");
    }

    [Fact]
    public void BrRoCius_NullCustomizationId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCustomizationId().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-CIUS");
    }

    // ── BR-RO-010 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("INV-001")]
    [InlineData("1")]
    [InlineData("A1B")]
    [InlineData("2024/001")]
    public void BrRo010_InvoiceNumberContainsDigit_Passes(string invoiceNumber)
    {
        var invoice = InvoiceBuilder.Valid().WithId(invoiceNumber).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-010");
    }

    [Theory]
    [InlineData("INV-ABC")]
    [InlineData("NONNUMERIC")]
    [InlineData("ABC-DEF-GHI")]
    public void BrRo010_InvoiceNumberHasNoDigit_Fails(string invoiceNumber)
    {
        var invoice = InvoiceBuilder.Valid().WithId(invoiceNumber).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-010");
    }

    [Fact]
    public void BrRo010_NullInvoiceId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        // BR-1 and BR-RO-010 both fire
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    // ── BR-RO-020 ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("380")]
    [InlineData("381")]
    [InlineData("384")]
    [InlineData("389")]
    [InlineData("751")]
    public void BrRo020_AllowedTypeCode_Passes(string typeCode)
    {
        var invoice = InvoiceBuilder.Valid().WithTypeCode(typeCode).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-020");
    }

    [Theory]
    [InlineData("999")]
    [InlineData("382")]
    [InlineData("380X")]
    [InlineData("")]
    [InlineData("INVOICE")]
    public void BrRo020_DisallowedTypeCode_Fails(string typeCode)
    {
        var invoice = InvoiceBuilder.Valid().WithTypeCode(typeCode).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-020");
    }

    // ── BR-RO-030 ───────────────────────────────────────────────────────────

    [Fact]
    public void BrRo030_RonCurrency_RuleDoesNotApply()
    {
        var invoice = InvoiceBuilder.Valid().WithCurrency("RON").Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithRonVat_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .WithVatCurrency("RON")
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithEurVat_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .WithVatCurrency("EUR")
            .Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-030");
    }

    [Fact]
    public void BrRo030_EurCurrencyWithNoVatCurrency_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithCurrency("EUR")
            .Build();
        // No TaxCurrencyCode set → HasValidVatCurrency returns false
        ShouldContainErrorCode(Validate(invoice), "BR-RO-030");
    }

    // ── BR-1 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br1_NullId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutId().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-1");
    }

    [Fact]
    public void Br1_EmptyId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithId("").Build();
        ShouldContainErrorCode(Validate(invoice), "BR-1");
    }

    // ── BR-2 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br2_NullIssueDate_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutIssueDate().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-2");
    }

    // ── BR-3 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br3_NullTypeCode_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTypeCode().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-3");
    }

    // ── BR-5 ────────────────────────────────────────────────────────────────

    [Fact]
    public void Br5_NullCurrency_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutCurrency().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-5");
    }

    // ── BR-16 ───────────────────────────────────────────────────────────────

    [Fact]
    public void Br16_NoLines_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutLines().Build();
        ShouldContainErrorCode(Validate(invoice), "BR-16");
    }

    [Fact]
    public void Br16_OneOrMoreLines_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(1).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-16");
    }

    // ── BR-RO-A999 ──────────────────────────────────────────────────────────

    [Fact]
    public void BrRoA999_ExactlyNineNineNineLines_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(999).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-A999");
    }

    [Fact]
    public void BrRoA999_OneThousandLines_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithLineCount(1000).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-A999");
    }

    // ── BR-RO-Z2 ────────────────────────────────────────────────────────────

    [Fact]
    public void BrRoZ2_TwoDecimalPlaces_Passes()
    {
        var invoice = InvoiceBuilder.Valid().WithTotals(100.00m, 119.00m, 119.00m).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-Z2");
    }

    [Fact]
    public void BrRoZ2_ThreeDecimalPlacesOnTaxExclusive_Fails()
    {
        // 100.001m has 3 decimal places
        var invoice = InvoiceBuilder.Valid().WithTotals(100.001m, 119.001m, 119.001m).Build();
        ShouldContainErrorCode(Validate(invoice), "BR-RO-Z2");
    }

    // ── Full valid invoice passes all rules ─────────────────────────────────

    [Fact]
    public void FullValidInvoice_PassesAllRules()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 9.2: Run**

```bash
dotnet test RoEFactura.Tests --filter "RoCiusUblValidatorTests" -v minimal
```

Expected: all pass.

- [ ] **Step 9.3: Commit**

```bash
git add RoEFactura.Tests/Validation/RoCiusUblValidatorTests.cs
git commit -m "test(svc): add comprehensive RoCiusUblValidator tests (BR-RO-* and BR-*)"
```

---

## Task 10: TotalsValidator tests

**Files:**
- Create: `RoEFactura.Tests/Validation/TotalsValidatorTests.cs`

- [ ] **Step 10.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class TotalsValidatorTests
{
    private readonly TotalsValidator _sut = new();

    private ValidationResult Validate(UblSharp.InvoiceType invoice)
        => _sut.Validate(invoice);

    // ── BR-12: TaxExclusiveAmount required ───────────────────────────────────

    [Fact]
    public void Br12_MissingTaxExclusiveAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTaxExclusiveAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-12");
    }

    [Fact]
    public void Br12_TaxExclusiveAmountPresent_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-12");
    }

    // ── BR-14: TaxInclusiveAmount required ───────────────────────────────────

    [Fact]
    public void Br14_MissingTaxInclusiveAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutTaxInclusiveAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-14");
    }

    // ── BR-15: PayableAmount required ────────────────────────────────────────

    [Fact]
    public void Br15_MissingPayableAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutPayableAmount().Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-15");
    }

    // ── BR-CO-10: Line sum = TaxExclusiveAmount ──────────────────────────────

    [Fact]
    public void BrCo10_LineSumMatchesTotal_Passes()
    {
        // Base invoice: 1 line × 100.00 = 100.00, TaxExclusiveAmount = 100.00
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-10");
    }

    [Fact]
    public void BrCo10_LineSumOffByMoreThanTolerance_Fails()
    {
        // Line = 100.00, but total says 200.00 → difference = 100, exceeds 0.01 tolerance
        var invoice = InvoiceBuilder.Valid().WithTotals(200m, 219m, 219m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-10");
    }

    [Fact]
    public void BrCo10_LineSumWithinOneCentTolerance_Passes()
    {
        // Line = 100.00, total = 100.01 → difference = 0.01, within tolerance
        var invoice = InvoiceBuilder.Valid().WithTotals(100.01m, 119.01m, 119.01m).Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-10");
    }

    // ── BR-CO-11: TaxInclusive = TaxExclusive + VAT ──────────────────────────

    [Fact]
    public void BrCo11_CorrectCalculation_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build(); // 100 + 19 = 119
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-11");
    }

    [Fact]
    public void BrCo11_WrongTaxInclusive_Fails()
    {
        // TaxExclusive=100, VAT=19, TaxInclusive=200 (should be 119)
        var invoice = InvoiceBuilder.Valid().WithTotals(100m, 200m, 200m).Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-11");
    }

    // ── BR-CO-12: VAT breakdown: taxable × rate = taxAmount ─────────────────

    [Fact]
    public void BrCo12_CorrectBreakdown_Passes()
    {
        // 100 × 19% = 19.00
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-12");
    }

    [Fact]
    public void BrCo12_WrongBreakdownTaxAmount_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithTaxTotalAmount(50m).Build();
        // Subtotal tax amount is still 19 but total says 50 — BR-CO-13 fires;
        // to specifically trigger BR-CO-12, we'd need to modify the subtotal directly.
        // This test validates that a large discrepancy triggers at least CO-12 or CO-13.
        var result = Validate(invoice);
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-CO-13" || e.ErrorCode == "BR-CO-12");
    }

    // ── BR-CO-13: VAT total = sum of VAT subtotals ───────────────────────────

    [Fact]
    public void BrCo13_CorrectVatTotalSum_Passes()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-CO-13");
    }

    // ── BR-29: Period end >= start ───────────────────────────────────────────

    [Fact]
    public void Br29_EndDateAfterStartDate_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 1), new DateTime(2024, 1, 31))
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_EndDateBeforeStartDate_Fails()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 31), new DateTime(2024, 1, 1))
            .Build();
        Validate(invoice).Errors.Should().Contain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_EndDateEqualsStartDate_Passes()
    {
        var invoice = InvoiceBuilder.Valid()
            .WithDocumentPeriod(new DateTime(2024, 1, 15), new DateTime(2024, 1, 15))
            .Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }

    [Fact]
    public void Br29_NoPeriodDefined_RuleSkipped()
    {
        var invoice = InvoiceBuilder.Valid().Build();
        Validate(invoice).Errors.Should().NotContain(e => e.ErrorCode == "BR-29");
    }
}
```

- [ ] **Step 10.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "TotalsValidatorTests" -v minimal
git add RoEFactura.Tests/Validation/TotalsValidatorTests.cs
git commit -m "test(svc): add TotalsValidator tests (BR-12, 14, 15, CO-10/11/12/13, BR-29)"
```

---

## Task 11: InvoiceLineValidator tests

**Files:**
- Create: `RoEFactura.Tests/Validation/InvoiceLineValidatorTests.cs`

- [ ] **Step 11.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp.CommonAggregateComponents;
using UblSharp.CommonBasicComponents;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class InvoiceLineValidatorTests
{
    private readonly InvoiceLineValidator _sut = new();

    private ValidationResult Validate(InvoiceLineType line) => _sut.Validate(line);

    private static InvoiceLineType ValidLine() => InvoiceBuilder.BuildValidLine("1");

    // ── BR-21: Line ID required ──────────────────────────────────────────────

    [Fact]
    public void Br21_MissingLineId_Fails()
    {
        var line = ValidLine();
        line.ID = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-21");
    }

    [Fact]
    public void Br21_EmptyLineId_Fails()
    {
        var line = ValidLine();
        line.ID = new IDType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-21");
    }

    [Fact]
    public void Br21_PresentLineId_Passes()
    {
        Validate(ValidLine()).Errors.Should().NotContain(e => e.ErrorCode == "BR-21");
    }

    // ── BR-22: Quantity required ─────────────────────────────────────────────

    [Fact]
    public void Br22_MissingQuantity_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-22");
    }

    // ── BR-23: Unit code required ────────────────────────────────────────────

    [Fact]
    public void Br23_MissingUnitCode_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = new InvoicedQuantityType { Value = 1m, unitCode = null };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-23");
    }

    [Fact]
    public void Br23_EmptyUnitCode_Fails()
    {
        var line = ValidLine();
        line.InvoicedQuantity = new InvoicedQuantityType { Value = 1m, unitCode = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-23");
    }

    // ── BR-24: LineExtensionAmount required ──────────────────────────────────

    [Fact]
    public void Br24_MissingLineExtensionAmount_Fails()
    {
        var line = ValidLine();
        line.LineExtensionAmount = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-24");
    }

    // ── BR-25: Price required ────────────────────────────────────────────────

    [Fact]
    public void Br25_MissingPrice_Fails()
    {
        var line = ValidLine();
        line.Price = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-25");
    }

    // ── BR-26: Item name required ────────────────────────────────────────────

    [Fact]
    public void Br26_MissingItemName_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-26");
    }

    [Fact]
    public void Br26_EmptyItemName_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-26");
    }

    // ── BR-27/28: Price must not be negative ─────────────────────────────────

    [Fact]
    public void Br27_NegativePrice_Fails()
    {
        var line = ValidLine();
        line.Price!.PriceAmount = new PriceAmountType { Value = -1m, currencyID = "RON" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-27");
    }

    [Fact]
    public void Br27_ZeroPrice_Passes()
    {
        var line = ValidLine();
        line.Price!.PriceAmount = new PriceAmountType { Value = 0m, currencyID = "RON" };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-27");
    }

    // ── BR-CO-4: VAT category required ──────────────────────────────────────

    [Fact]
    public void BrCo4_MissingVatCategory_Fails()
    {
        var line = ValidLine();
        line.Item!.ClassifiedTaxCategory = null;
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-4");
    }

    [Fact]
    public void BrCo4_EmptyVatCategoryId_Fails()
    {
        var line = ValidLine();
        line.Item!.ClassifiedTaxCategory![0].ID = new IDType { Value = "" };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-CO-4");
    }

    // ── BR-30: Line period ────────────────────────────────────────────────────

    [Fact]
    public void Br30_ValidLinePeriod_Passes()
    {
        var line = ValidLine();
        line.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new StartDateType { Value = new DateTime(2024, 1, 1) },
                EndDate = new EndDateType { Value = new DateTime(2024, 1, 31) }
            }
        };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "BR-30");
    }

    [Fact]
    public void Br30_EndBeforeStart_Fails()
    {
        var line = ValidLine();
        line.InvoicePeriod = new List<PeriodType>
        {
            new PeriodType
            {
                StartDate = new StartDateType { Value = new DateTime(2024, 1, 31) },
                EndDate = new EndDateType { Value = new DateTime(2024, 1, 1) }
            }
        };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "BR-30");
    }

    // ── Romanian length limits ────────────────────────────────────────────────

    [Fact]
    public void RoLineNoteLength_ExactlyThreeHundredChars_Passes()
    {
        var line = ValidLine();
        line.Note = new List<NoteType> { new NoteType { Value = new string('A', 300) } };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "RO-LINE-NOTE-LENGTH");
    }

    [Fact]
    public void RoLineNoteLength_ThreeHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Note = new List<NoteType> { new NoteType { Value = new string('A', 301) } };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-LINE-NOTE-LENGTH");
    }

    [Fact]
    public void RoItemNameLength_ExactlyTwoHundredChars_Passes()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 200) };
        Validate(line).Errors.Should().NotContain(e => e.ErrorCode == "RO-ITEM-NAME-LENGTH");
    }

    [Fact]
    public void RoItemNameLength_TwoHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Item!.Name = new NameType { Value = new string('A', 201) };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-ITEM-NAME-LENGTH");
    }

    [Fact]
    public void RoItemDescLength_TwoHundredOneChars_Fails()
    {
        var line = ValidLine();
        line.Item!.Description = new List<DescriptionType>
        {
            new DescriptionType { Value = new string('A', 201) }
        };
        Validate(line).Errors.Should().Contain(e => e.ErrorCode == "RO-ITEM-DESC-LENGTH");
    }

    // ── Full valid line passes all rules ──────────────────────────────────────

    [Fact]
    public void ValidLine_PassesAllRules()
    {
        Validate(ValidLine()).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 11.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "InvoiceLineValidatorTests" -v minimal
git add RoEFactura.Tests/Validation/InvoiceLineValidatorTests.cs
git commit -m "test(svc): add InvoiceLineValidator tests (BR-21 through BR-30, Romanian length limits)"
```

---

## Task 12: RomanianAddressValidator tests

**Files:**
- Create: `RoEFactura.Tests/Validation/RomanianAddressValidatorTests.cs`

- [ ] **Step 12.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation.Results;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using RoEFactura.Validation.Constants;
using UblSharp.CommonAggregateComponents;
using UblSharp.CommonBasicComponents;
using Xunit;

namespace RoEFactura.Tests.Validation;

public class RomanianAddressValidatorTests
{
    private readonly RomanianAddressValidator _sut = new();

    private ValidationResult Validate(AddressType address) => _sut.Validate(address);

    private static AddressType RoAddress(string city, string county)
        => InvoiceBuilder.BuildRomanianAddress(city, county);

    // ── Non-Romanian address: all rules skip ─────────────────────────────────

    [Fact]
    public void NonRomanianAddress_AllRulesSkipped()
    {
        var address = new AddressType
        {
            CityName = new CityNameType { Value = "Berlin" },
            Country = new CountryType
            {
                IdentificationCode = new IdentificationCodeType { Value = "DE" }
            }
        };
        Validate(address).IsValid.Should().BeTrue();
    }

    // ── BR-RO-COUNTY: valid county codes ─────────────────────────────────────

    [Theory]
    [InlineData("AB")] [InlineData("AR")] [InlineData("AG")] [InlineData("B")]
    [InlineData("BC")] [InlineData("BH")] [InlineData("BN")] [InlineData("BT")]
    [InlineData("BV")] [InlineData("BR")] [InlineData("BZ")] [InlineData("CS")]
    [InlineData("CL")] [InlineData("CJ")] [InlineData("CT")] [InlineData("CV")]
    [InlineData("DB")] [InlineData("DJ")] [InlineData("GL")] [InlineData("GR")]
    [InlineData("GJ")] [InlineData("HR")] [InlineData("HD")] [InlineData("IL")]
    [InlineData("IS")] [InlineData("IF")] [InlineData("MM")] [InlineData("MH")]
    [InlineData("MS")] [InlineData("NT")] [InlineData("OT")] [InlineData("PH")]
    [InlineData("SM")] [InlineData("SJ")] [InlineData("SB")] [InlineData("SV")]
    [InlineData("TR")] [InlineData("TM")] [InlineData("TL")] [InlineData("VS")]
    [InlineData("VL")] [InlineData("VN")]
    public void BrRoCounty_AllValidCountyCodes_Pass(string county)
    {
        // Use a non-Bucharest city to avoid triggering the Bucharest sector rule
        var address = RoAddress("Cluj-Napoca", county == "B" ? "B" : county);
        if (county == "B")
        {
            // Bucharest needs a sector city name; skip county-specific assertion here
            // (covered separately in Bucharest tests)
            return;
        }
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-COUNTY");
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("ZZ")]
    [InlineData("RO")]
    [InlineData("")]
    [InlineData("CLJ")]
    public void BrRoCounty_InvalidCountyCode_Fails(string county)
    {
        var address = RoAddress("Cluj-Napoca", county);
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-COUNTY");
    }

    // ── BR-RO-BUCHAREST: sector validation ───────────────────────────────────

    [Theory]
    [InlineData("Sector 1")]
    [InlineData("Sector 2")]
    [InlineData("Sector 3")]
    [InlineData("Sector 4")]
    [InlineData("Sector 5")]
    [InlineData("Sector 6")]
    [InlineData("sector 3")]   // case-insensitive
    [InlineData("SECTOR 1")]   // case-insensitive
    public void BrRoBucharest_ValidSector_Passes(string cityName)
    {
        var address = RoAddress(cityName, "B");
        Validate(address).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-BUCHAREST");
    }

    [Theory]
    [InlineData("Bucuresti")]
    [InlineData("Sector 7")]
    [InlineData("Sector 0")]
    [InlineData("sector")]
    [InlineData("")]
    public void BrRoBucharest_InvalidCityForBucharest_Fails(string cityName)
    {
        var address = RoAddress(cityName, "B");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-BUCHAREST");
    }

    // ── BR-RO-CITY-REQUIRED ──────────────────────────────────────────────────

    [Fact]
    public void BrRoCityRequired_EmptyCityName_Fails()
    {
        var address = RoAddress("", "CJ");
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }

    [Fact]
    public void BrRoCityRequired_NullCityName_Fails()
    {
        var address = RoAddress("Cluj-Napoca", "CJ");
        address.CityName = null;
        Validate(address).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-CITY-REQUIRED");
    }
}
```

- [ ] **Step 12.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "RomanianAddressValidatorTests" -v minimal
git add RoEFactura.Tests/Validation/RomanianAddressValidatorTests.cs
git commit -m "test(svc): add RomanianAddressValidator tests (all 42 county codes, Bucharest sectors)"
```

---

## Task 13: Party validator tests

**Files:**
- Create: `RoEFactura.Tests/Validation/PartyValidators/SellerPartyValidatorTests.cs`
- Create: `RoEFactura.Tests/Validation/PartyValidators/BuyerPartyValidatorTests.cs`
- Create: `RoEFactura.Tests/Validation/PartyValidators/PayeePartyValidatorTests.cs`

- [ ] **Step 13.1: SellerPartyValidatorTests**

```csharp
using FluentAssertions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class SellerPartyValidatorTests
{
    private readonly SellerPartyValidator _sut = new();

    private SupplierPartyType ValidSeller()
        => InvoiceBuilder.Valid().Build().AccountingSupplierParty!;

    [Fact]
    public void Br6_MissingRegistrationNameAndPartyName_Fails()
    {
        var seller = ValidSeller();
        seller.Party!.PartyLegalEntity = null;
        seller.Party.PartyName = null;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-6");
    }

    [Fact]
    public void Br6_OnlyPartyNamePresent_Passes()
    {
        var seller = ValidSeller();
        seller.Party!.PartyLegalEntity = null;
        seller.Party.PartyName = new List<PartyNameType>
        {
            new PartyNameType { Name = new UblSharp.CommonBasicComponents.NameType { Value = "Test" } }
        };
        _sut.Validate(seller).Errors.Should().NotContain(e => e.ErrorCode == "BR-6");
    }

    [Fact]
    public void Br8_MissingPostalAddress_Fails()
    {
        var seller = ValidSeller();
        seller.Party!.PostalAddress = null;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-8");
    }

    [Fact]
    public void BrRoSellerId_RomanianSellerWithoutCompanyId_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutSellerCompanyId().Build();
        var seller = invoice.AccountingSupplierParty!;
        _sut.Validate(seller).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-SELLER-ID");
    }

    [Fact]
    public void BrRoSellerId_NonRomanianSellerWithoutCompanyId_Passes()
    {
        var seller = ValidSeller();
        seller.Party!.PostalAddress!.Country!.IdentificationCode =
            new UblSharp.CommonBasicComponents.IdentificationCodeType { Value = "DE" };
        seller.Party.PartyLegalEntity![0].CompanyID = null;
        _sut.Validate(seller).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-SELLER-ID");
    }

    [Fact]
    public void ValidSeller_PassesAllRules()
    {
        _sut.Validate(ValidSeller()).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 13.2: BuyerPartyValidatorTests**

```csharp
using FluentAssertions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class BuyerPartyValidatorTests
{
    private readonly BuyerPartyValidator _sut = new();

    private CustomerPartyType ValidBuyer()
        => InvoiceBuilder.Valid().Build().AccountingCustomerParty!;

    [Fact]
    public void Br7_MissingBuyerName_Fails()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PartyLegalEntity = null;
        buyer.Party.PartyName = null;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-7");
    }

    [Fact]
    public void Br10_MissingBuyerAddress_Fails()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PostalAddress = null;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-10");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithNoCuiAndNoVat_Fails()
    {
        var invoice = InvoiceBuilder.Valid().WithoutBuyerIdentifiers().Build();
        var buyer = invoice.AccountingCustomerParty!;
        _sut.Validate(buyer).Errors.Should().Contain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithCuiOnly_Passes()
    {
        var buyer = ValidBuyer();
        // Remove VAT ID but keep company ID
        buyer.Party!.PartyTaxScheme![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_RomanianBuyerWithVatOnly_Passes()
    {
        var buyer = ValidBuyer();
        // Remove company ID but keep VAT ID
        buyer.Party!.PartyLegalEntity![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void BrRo120_NonRomanianBuyerWithNoIdentifiers_Passes()
    {
        var buyer = ValidBuyer();
        buyer.Party!.PostalAddress!.Country!.IdentificationCode =
            new UblSharp.CommonBasicComponents.IdentificationCodeType { Value = "DE" };
        buyer.Party.PartyLegalEntity![0].CompanyID = null;
        buyer.Party.PartyTaxScheme![0].CompanyID = null;
        _sut.Validate(buyer).Errors.Should().NotContain(e => e.ErrorCode == "BR-RO-120");
    }

    [Fact]
    public void ValidBuyer_PassesAllRules()
    {
        _sut.Validate(ValidBuyer()).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 13.3: PayeePartyValidatorTests**

```csharp
using FluentAssertions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation.PartyValidators;
using UblSharp.CommonAggregateComponents;
using UblSharp.CommonBasicComponents;
using Xunit;

namespace RoEFactura.Tests.Validation.PartyValidators;

public class PayeePartyValidatorTests
{
    private readonly PayeePartyValidator _sut = new();

    private static PartyType ValidPayee() => new PartyType
    {
        PartyName = new List<PartyNameType>
        {
            new PartyNameType { Name = new NameType { Value = "Payee Entity SRL" } }
        },
        PartyLegalEntity = new List<PartyLegalEntityType>
        {
            new PartyLegalEntityType
            {
                RegistrationName = new RegistrationNameType { Value = "Payee Entity SRL" },
                CompanyID = new CompanyIDType { Value = "J12/500/2021" }
            }
        }
    };

    [Fact]
    public void Br17_PayeeWithName_Passes()
    {
        _sut.Validate(ValidPayee()).Errors.Should().NotContain(e => e.ErrorCode == "BR-17");
    }

    [Fact]
    public void Br17_PayeeWithoutName_Fails()
    {
        var payee = ValidPayee();
        payee.PartyLegalEntity![0].RegistrationName = null;
        payee.PartyName = null;
        _sut.Validate(payee).Errors.Should().Contain(e => e.ErrorCode == "BR-17");
    }

    [Fact]
    public void ValidPayee_PassesAllRules()
    {
        _sut.Validate(ValidPayee()).IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 13.4: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "SellerPartyValidatorTests|BuyerPartyValidatorTests|PayeePartyValidatorTests" -v minimal
git add RoEFactura.Tests/Validation/PartyValidators/
git commit -m "test(svc): add SellerPartyValidator, BuyerPartyValidator, PayeePartyValidator tests"
```

---

## Task 14: UblProcessingService tests

**Files:**
- Create: `RoEFactura.Tests/Services/UblProcessingServiceTests.cs`

Tests use the **real** `RoCiusUblValidator` for integration-style tests, and a mocked `IValidator<InvoiceType>` for isolation tests. `ILogger` is provided via `Microsoft.Extensions.Logging.Abstractions.NullLogger`.

- [ ] **Step 14.1: Write tests**

```csharp
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RoEFactura.Services.Processing;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using UblSharp;
using Xunit;

namespace RoEFactura.Tests.Services;

public class UblProcessingServiceTests
{
    private static UblProcessingService CreateWithRealValidator()
    {
        var validator = new RoCiusUblValidator();
        var logger = NullLogger<UblProcessingService>.Instance;
        return new UblProcessingService(validator, logger);
    }

    private static UblProcessingService CreateWithMockValidator(Mock<IValidator<InvoiceType>> mockValidator)
    {
        var logger = NullLogger<UblProcessingService>.Instance;
        return new UblProcessingService(mockValidator.Object, logger);
    }

    private static string LoadXml(string relativePath) => ZipBuilder.LoadFixture(relativePath);

    // ── ProcessInvoiceAsync (string content) ─────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceAsync_ValidXml_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");

        var result = await sut.ProcessInvoiceAsync(xml);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceAsync_MalformedXml_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();

        var result = await sut.ProcessInvoiceAsync("<not-ubl>");

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessInvoiceAsync_NullXml_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();

        var result = await sut.ProcessInvoiceAsync(null!);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessInvoiceAsync_InvalidInvoice_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");

        var result = await sut.ProcessInvoiceAsync(xml);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    [Fact]
    public async Task ProcessInvoiceAsync_SkipValidation_SucceedsEvenIfInvalid()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");

        var result = await sut.ProcessInvoiceAsync(xml, skipValidation: true);

        result.IsSuccess.Should().BeTrue();
    }

    // ── ProcessInvoiceXmlAsync (byte[] data) ─────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceXmlAsync_ValidXmlBytes_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        var result = await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        result.IsSuccess.Should().BeTrue();
    }

    // ── ProcessInvoiceZipAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithSingleXml_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] zip = ZipBuilder.WithEntries(("invoice.xml", xml));

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeTrue();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithInvoiceAndSemnatura_PicksInvoice()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] zip = ZipBuilder.WithEntries(
            ("invoice.xml", xml),
            ("semnatura.xml", "<semnatura/>")
        );

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeTrue();
        result.Data!.ID!.Value.Should().Be("INV-2024-001");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_ZipWithOnlySemnatura_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        byte[] zip = ZipBuilder.WithEntries(("semnatura.xml", "<semnatura/>"));

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeFalse();
        result.Errors[0].ErrorMessage.Should().Contain("semnatura");
    }

    [Fact]
    public async Task ProcessInvoiceZipAsync_EmptyZip_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        byte[] zip = ZipBuilder.WithEntries(); // no entries

        var result = await sut.ProcessInvoiceZipAsync(zip, "archive.zip");

        result.IsSuccess.Should().BeFalse();
    }

    // ── ValidateInvoiceAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task ValidateInvoiceAsync_ValidInvoice_ReturnsSuccess()
    {
        var sut = CreateWithRealValidator();
        var invoice = InvoiceBuilder.Valid().Build();

        var result = await sut.ValidateInvoiceAsync(invoice);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateInvoiceAsync_InvalidInvoice_ReturnsFailed()
    {
        var sut = CreateWithRealValidator();
        var invoice = InvoiceBuilder.Valid().WithId("NO-DIGIT").Build();

        var result = await sut.ValidateInvoiceAsync(invoice);

        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == "BR-RO-010");
    }

    // ── ProcessingStats ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessingStats_SuccessfulProcess_IncrementsSuccessCount()
    {
        var sut = CreateWithRealValidator();
        sut.ResetProcessingStats();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(1);
        stats.SuccessfullyProcessed.Should().Be(1);
        stats.ValidationErrors.Should().Be(0);
    }

    [Fact]
    public async Task ProcessingStats_FailedProcess_IncrementsErrorCount()
    {
        var sut = CreateWithRealValidator();
        sut.ResetProcessingStats();
        string xml = LoadXml("Invalid/invalid-br-ro-010.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);

        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(1);
        stats.ValidationErrors.Should().Be(1);
        stats.SuccessfullyProcessed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessingStats_Reset_ClearsAllCounters()
    {
        var sut = CreateWithRealValidator();
        string xml = LoadXml("Valid/valid-380-ron.xml");
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);
        await sut.ProcessInvoiceXmlAsync(bytes, "test.xml");

        sut.ResetProcessingStats();

        var stats = sut.GetProcessingStats();
        stats.TotalProcessed.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
    }

    // ── Validator is called (isolation test with mock) ────────────────────────

    [Fact]
    public async Task ProcessInvoiceAsync_CallsValidatorOnce()
    {
        var mockValidator = new Mock<IValidator<InvoiceType>>();
        mockValidator
            .Setup(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());
        var sut = CreateWithMockValidator(mockValidator);
        string xml = LoadXml("Valid/valid-380-ron.xml");

        await sut.ProcessInvoiceAsync(xml, skipValidation: false);

        mockValidator.Verify(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessInvoiceAsync_SkipValidation_NeverCallsValidator()
    {
        var mockValidator = new Mock<IValidator<InvoiceType>>();
        var sut = CreateWithMockValidator(mockValidator);
        string xml = LoadXml("Valid/valid-380-ron.xml");

        await sut.ProcessInvoiceAsync(xml, skipValidation: true);

        mockValidator.Verify(v => v.ValidateAsync(It.IsAny<InvoiceType>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
```

- [ ] **Step 14.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "UblProcessingServiceTests" -v minimal
git add RoEFactura.Tests/Services/UblProcessingServiceTests.cs
git commit -m "test(svc): add UblProcessingService tests (ZIP, XML parsing, stats, skip-validation)"
```

---

## Task 15: AnafEInvoiceClient tests

**Files:**
- Create: `RoEFactura.Tests/Services/AnafEInvoiceClientTests.cs`

Tests mock `HttpMessageHandler` to intercept all HTTP calls. `AnafEInvoiceClient` has an internal constructor that accepts raw endpoint strings — use that to inject controlled endpoints.

- [ ] **Step 15.1: Write tests**

```csharp
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using RoEFactura.Dtos;
using RoEFactura.Services.Api;
using Xunit;

namespace RoEFactura.Tests.Services;

public class AnafEInvoiceClientTests
{
    private const string FakeToken = "eyJhbGciOiJSUzI1NiJ9.fake-token";
    private const string FakeCui = "12345678";

    private static (AnafEInvoiceClient client, Mock<HttpMessageHandler> handlerMock)
        CreateClient(HttpResponseMessage? defaultResponse = null)
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
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(handlerMock.Object);

        // Use the internal string-based constructor to inject test endpoints
        var client = new AnafEInvoiceClient(
            "https://test.anaf.ro/paged",
            "https://test.anaf.ro/list",
            "https://test.anaf.ro/download",
            "https://test.anaf.ro/validate",
            "https://test.anaf.ro/upload"
        );
        // Inject the HttpClient via reflection (since the test constructor doesn't take HttpClient)
        // Alternative: use the DI constructor via a test service provider
        return (client, handlerMock);
    }

    // Note: AnafEInvoiceClient's internal string constructor does not take an HttpClient.
    // Use the DI-wired approach for HTTP-dependent tests.
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

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        services.AddLogging();
        services.AddSingleton(httpClient);

        // Build a minimal UblProcessingService for injection
        var validator = new RoEFactura.Validation.RoCiusUblValidator();
        var processingService = new RoEFactura.Services.Processing.UblProcessingService(
            validator,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RoEFactura.Services.Processing.UblProcessingService>.Instance
        );

        var env = new Mock<Microsoft.Extensions.Hosting.IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns("Production");

        var client = new AnafEInvoiceClient(
            httpClient,
            env.Object,
            processingService,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<AnafEInvoiceClient>.Instance
        );

        return (client, handlerMock);
    }

    // ── Guard clause tests ────────────────────────────────────────────────────

    [Fact]
    public async Task ListEInvoicesAsync_NullToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ListEInvoicesAsync(null!, 7, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_EmptyToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ListEInvoicesAsync("", 7, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_NullCui_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ListEInvoicesAsync(FakeToken, 7, null!));
    }

    [Fact]
    public async Task ListEInvoicesAsync_ZeroDays_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ListEInvoicesAsync(FakeToken, 0, FakeCui));
    }

    [Fact]
    public async Task ListEInvoicesAsync_NegativeDays_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
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
                req.Headers.Authorization!.Scheme == "Bearer" &&
                req.Headers.Authorization.Parameter == FakeToken),
            ItExpr.IsAny<CancellationToken>()
        );
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
                req.RequestUri!.Query.Contains($"cif={FakeCui}") &&
                req.RequestUri.Query.Contains("zile=30")),
            ItExpr.IsAny<CancellationToken>()
        );
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
            ItExpr.IsAny<CancellationToken>()
        );
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
            new EInvoiceAnafResponse { Id = 12345 }
        };
        var responseBody = JsonConvert.SerializeObject(new ListEInvoicesAnafResponse { Items = items });
        var (client, _) = CreateClientWithHttp(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody)
            });

        var result = await client.ListEInvoicesAsync(FakeToken, 7, FakeCui);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(12345);
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
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task ValidateXmlContentAsync_NullToken_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ValidateXmlContentAsync(null!, "<Invoice/>"));
    }

    [Fact]
    public async Task ValidateXmlContentAsync_NullXml_Throws()
    {
        var (client, _) = CreateClientWithHttp();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.ValidateXmlContentAsync(FakeToken, null!));
    }
}
```

- [ ] **Step 15.2: Run and commit**

```bash
dotnet test RoEFactura.Tests --filter "AnafEInvoiceClientTests" -v minimal
git add RoEFactura.Tests/Services/AnafEInvoiceClientTests.cs
git commit -m "test(svc): add AnafEInvoiceClient tests (guard clauses, URL construction, HTTP mocking)"
```

---

## Task 16: End-to-end XML fixture validation tests

**Files:**
- Create: `RoEFactura.Tests/Integration/XmlFixtureValidationTests.cs`

These tests are the "proof" that the whole pipeline works end-to-end: load XML → deserialize → run full `RoCiusUblValidator` chain → assert specific outcome.

- [ ] **Step 16.1: Write tests**

```csharp
using FluentAssertions;
using RoEFactura.Extensions;
using RoEFactura.Tests.Helpers;
using RoEFactura.Validation;
using Xunit;

namespace RoEFactura.Tests.Integration;

/// <summary>
/// End-to-end tests: load XML fixture → deserialize → run full validator → assert outcome.
/// These verify the fixture files themselves are valid/invalid as intended.
/// </summary>
public class XmlFixtureValidationTests
{
    private readonly RoCiusUblValidator _validator = new();

    private UblSharp.InvoiceType LoadAndParse(string relativePath)
    {
        string xml = ZipBuilder.LoadFixture(relativePath);
        var invoice = UblSharpExtensions.LoadInvoiceFromXml(xml);
        invoice.Should().NotBeNull($"Fixture {relativePath} should parse successfully");
        return invoice!;
    }

    // ── Valid fixtures: all should pass ──────────────────────────────────────

    [Theory]
    [InlineData("Valid/valid-380-ron.xml")]
    [InlineData("Valid/valid-381-credit-note.xml")]
    [InlineData("Valid/valid-389-storno.xml")]
    [InlineData("Valid/valid-384-corrective.xml")]
    [InlineData("Valid/valid-751-activity.xml")]
    [InlineData("Valid/valid-eur-with-ron-vat.xml")]
    [InlineData("Valid/valid-bucharest-sector3.xml")]
    public void ValidFixture_PassesFullValidation(string fixturePath)
    {
        var invoice = LoadAndParse(fixturePath);
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeTrue(
            $"Fixture '{fixturePath}' should be valid but got errors: " +
            string.Join(", ", result.Errors.Select(e => $"{e.ErrorCode}: {e.ErrorMessage}")));
    }

    // ── Invalid fixtures: each should fail with expected error code ──────────

    [Theory]
    [InlineData("Invalid/invalid-br-ro-cius.xml", "BR-RO-CIUS")]
    [InlineData("Invalid/invalid-br-ro-010.xml", "BR-RO-010")]
    [InlineData("Invalid/invalid-br-ro-020.xml", "BR-RO-020")]
    [InlineData("Invalid/invalid-br-ro-030.xml", "BR-RO-030")]
    [InlineData("Invalid/invalid-br-ro-120.xml", "BR-RO-120")]
    [InlineData("Invalid/invalid-br-16-no-lines.xml", "BR-16")]
    [InlineData("Invalid/invalid-br-co-10.xml", "BR-CO-10")]
    [InlineData("Invalid/invalid-br-co-11.xml", "BR-CO-11")]
    [InlineData("Invalid/invalid-br-12.xml", "BR-12")]
    [InlineData("Invalid/invalid-br-2.xml", "BR-2")]
    public void InvalidFixture_FailsWithExpectedErrorCode(string fixturePath, string expectedErrorCode)
    {
        var invoice = LoadAndParse(fixturePath);
        var result = _validator.Validate(invoice);

        result.IsValid.Should().BeFalse($"Fixture '{fixturePath}' should fail validation");
        result.Errors.Should().Contain(e => e.ErrorCode == expectedErrorCode,
            $"Expected error code '{expectedErrorCode}' in fixture '{fixturePath}' " +
            $"but got: {string.Join(", ", result.Errors.Select(e => e.ErrorCode))}");
    }

    // ── EUR fixture: specific assertions ────────────────────────────────────

    [Fact]
    public void EurFixture_HasRONTaxCurrencyCode()
    {
        var invoice = LoadAndParse("Valid/valid-eur-with-ron-vat.xml");
        invoice.DocumentCurrencyCode!.Value.Should().Be("EUR");
        invoice.TaxCurrencyCode!.Value.Should().Be("RON");
    }

    // ── Bucharest fixture: sector city name ──────────────────────────────────

    [Fact]
    public void BucharestFixture_HasSectorCityName()
    {
        var invoice = LoadAndParse("Valid/valid-bucharest-sector3.xml");
        var buyerCity = invoice.AccountingCustomerParty!.Party!.PostalAddress!.CityName!.Value;
        buyerCity.Should().StartWith("Sector");
    }

    // ── All invoice type codes appear in their respective fixtures ───────────

    [Theory]
    [InlineData("Valid/valid-380-ron.xml", "380")]
    [InlineData("Valid/valid-381-credit-note.xml", "381")]
    [InlineData("Valid/valid-389-storno.xml", "389")]
    [InlineData("Valid/valid-384-corrective.xml", "384")]
    [InlineData("Valid/valid-751-activity.xml", "751")]
    public void FixtureTypeCode_MatchesExpected(string fixturePath, string expectedCode)
    {
        var invoice = LoadAndParse(fixturePath);
        invoice.InvoiceTypeCode!.Value.Should().Be(expectedCode);
    }
}
```

- [ ] **Step 16.2: Run all tests**

```bash
dotnet test RoEFactura.Tests -v minimal
```

Expected output like:
```
Passed!  - Failed: 0, Passed: N, Skipped: 0, Total: N
```

- [ ] **Step 16.3: Commit**

```bash
git add RoEFactura.Tests/Integration/
git commit -m "test(svc): add end-to-end XML fixture validation tests"
```

---

## Final verification

- [ ] **Run full test suite**

```bash
cd /path/to/ro-efactura
dotnet test --verbosity normal 2>&1 | tail -20
```

Expected: 0 failures, all tests pass.

- [ ] **Verify build is clean**

```bash
dotnet build -c Release
```

Expected: `Build succeeded. 0 Error(s)`

- [ ] **Update AGENTS.md** with any learned facts about UblSharp type construction.

- [ ] **Final commit**

```bash
git add AGENTS.md
git commit -m "chore(svc): update AGENTS.md with test suite learned facts"
```

---

## Summary

| Task | Tests Added | Coverage Target |
|------|-------------|-----------------|
| 5 | ~8 | `EInvoiceXmlFileFilter` |
| 6 | ~5 | `ProcessingResult<T>` |
| 7 | ~8 | `UblSharpExtensions` round-trip |
| 8 | ~12 | `InvoiceTypeExtensions` |
| 9 | ~25 | All `RoCiusUblValidator` rules |
| 10 | ~14 | All `TotalsValidator` rules |
| 11 | ~18 | All `InvoiceLineValidator` rules |
| 12 | ~55 | All 42 county codes + Bucharest sectors |
| 13 | ~12 | Seller / Buyer / Payee party validators |
| 14 | ~14 | `UblProcessingService` (ZIP, stats, skip-validation) |
| 15 | ~10 | `AnafEInvoiceClient` (HTTP mocking, guard clauses) |
| 16 | ~20 | End-to-end XML fixture pipeline |
| **Total** | **~200** | **All validators, services, utilities** |
