# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

RoEFactura is a .NET library (targeting `net10.0`) for integrating with the Romanian ANAF (National
Agency for Fiscal Administration) e-invoicing system. It implements the official ANAF e-Factura REST
contract (upload/uploadb2c, `stareMesaj`, list/paged list, `descarcare`, public `validare`/`transformare`),
dual authentication (certificate-based and OAuth web flow, including refresh tokens), and complete UBL
2.1 document processing with CIUS-RO 1.0.1 validation.

## Build and Development Commands

```bash
# Build the project
dotnet build RoEFactura.sln

# Build in Release mode
dotnet build RoEFactura.sln -c Release

# Restore NuGet packages
dotnet restore

# Clean build artifacts
dotnet clean

# Run the test suite (xUnit, FluentAssertions, Moq)
dotnet test RoEFactura.Tests/RoEFactura.Tests.csproj

# Create NuGet package
dotnet pack RoEFactura/RoEFactura.csproj
```

## Architecture

### Core Components

1. **Authentication Layer** (`Services/Authentication/`)
   - `AnafOAuthClient`: Dual authentication support
     - **Certificate-based**: Traditional desktop app authentication using Romanian digital certificates
     - **OAuth web flow**: Modern web application integration, including access-token refresh
       (`RefreshAccessTokenAsync`)
   - `Token`: JWT token model for API authentication, with computed `ExpiresAtUtc`/`RefreshTokenExpiresAtUtc`
   - `CertificateInfo`: Certificate metadata and validation
   - Certificate validation supports multiple Romanian certificate providers (CERTSIGN, DIGISIGN, ALFASIGN, etc.)
   - State management for CSRF protection in OAuth flows

2. **API Client** (`Services/Api/`)
   - `AnafEInvoiceClient` (`internal sealed`, behind `IAnafEInvoiceClient`): the full official ANAF
     e-Factura contract:
     - `UploadAsync`/`GetMessageStatusAsync`/`DownloadMessageAsync` (authenticated, Bearer token,
       environment-dependent base URL)
     - `ValidateWithAnafAsync`/`ConvertToPdfAsync` (public, stateless, no auth, always production)
     - `ListEInvoicesAsync`/`ListPagedEInvoicesAsync`/`ProcessDownloadedInvoiceAsync`/`ProcessMultipleInvoicesAsync`
       (kept from 1.x, each with a new `CancellationToken`-required overload)
     - `DownloadEInvoiceAsync` (`[Obsolete]`; writes to disk, kept for 1.x compatibility)
   - Resolves its base URL once per instance from `IOptions<RoEFacturaOptions>` (`ResolveApiBaseUrl()`/
     `ResolvePublicServicesBaseUrl()`); registered as a typed `HttpClient`
     (`AddHttpClient<IAnafEInvoiceClient, AnafEInvoiceClient>()`).
   - Non-2xx responses throw `AnafApiException` (429 → the `AnafRateLimitException` subclass), both of
     which subclass `HttpRequestException`.

3. **UBL Processing Engine** (`Services/Processing/`)
   - `UblProcessingService`: Complete UBL 2.1 document processing pipeline
     - XML parsing and deserialization (BOM-safe)
     - ZIP archive extraction and processing
     - CIUS-RO 1.0.1 validation
     - Processing statistics **per service instance** (no static/shared state) and monitoring
     - Batch processing support

4. **Validation Engine** (`Validation/`)
   - `RoCiusUblValidator`: EN 16931 core + CIUS-RO 1.0.1 compliance validation (public parameterless
     constructor kept for callers that construct it directly)
   - Romanian-specific validators for parties, addresses (`RomanianAddressValidator`, role-based
     Seller/Buyer), invoice lines, and VAT-category exemption reasons (`VatBreakdownValidator`)
   - `RomanianConstants`: county codes (`RO-`-prefixed ISO 3166-2:RO), invoice type codes, VAT point
     date codes, length limits
   - `Validation/Constants/RoCiusRuleIds`: centralized rule-id constants
   - See [docs/VALIDATION_RULES.md](docs/VALIDATION_RULES.md) for the full rule-by-rule reference,
     sources (official CIUS-RO 1.0.9 schematron), and documented tolerances/leniencies. Local validation
     is a fast pre-check; `IAnafEInvoiceClient.ValidateWithAnafAsync` remains authoritative.

5. **Extensions** (`Extensions/`)
   - `InvoiceTypeExtensions`: Rich invoice analysis capabilities
     - Romanian invoice detection (current CIUS-RO 1.0.1 id, or the legacy RO_CIUS 1.0.0.2021 id)
     - Currency and totals extraction
     - Validation summaries
   - `PartyExtensions`: Party information processing
   - `UblSharpExtensions`: UBL document manipulation utilities, including `SaveInvoiceToXml` (UTF-8
     declaration) and `SaveInvoiceToXmlBytes` (UTF-8 without BOM)

6. **Models and DTOs** (`Models/`, `Dtos/`)
   - `RoEFacturaOptions`: `Environment` (`AnafEnvironment.Test`/`Production`), `ApiBaseUrl` override,
     `PublicServicesBaseUrl`
   - `AnafOAuthOptions`: Configurable OAuth settings
   - `OAuthModels`: Complete OAuth flow models (initiation, token exchange, status)
   - `AnafUploadOptions`/`AnafUploadResult`/`AnafMessageStatusResult`/`AnafDownloadResult`/`AnafValidationResult`:
     typed request/response models for the official contract
   - `AnafApiException`/`AnafRateLimitException`/`AnafDownloadWindowExpiredException`: typed ANAF errors
   - `ProcessingResult<T>`: Generic result wrapper with validation errors
   - ANAF API response DTOs (`System.Text.Json`, `[JsonPropertyName]`) for the list endpoints

7. **Dependency Injection** (`ServiceCollectionExtensions.cs`)
   - **Registration patterns**:
     - `AddRoEFactura(IConfiguration? configuration = null)`: binds the `RoEFactura` section when
       `configuration` is supplied; a missing section (or no `configuration` at all) resolves to TEST
     - `AddRoEFactura(Action<RoEFacturaOptions> configure)`: programmatic configuration, no `IConfiguration` needed
     - `AddRoEFacturaWithOAuth(options)`: OAuth with direct configuration
     - `AddRoEFacturaWithOAuth(configuration, sectionName = "AnafOAuth")`: OAuth with appsettings.json binding
   - Uses the `HttpClient` factory pattern (`AddHttpClient<IAnafEInvoiceClient, AnafEInvoiceClient>()`)
   - FluentValidation integration for all validators

8. **Utilities** (`Utilities/`)
   - `XmlFileDeserializer`: Generic XML deserialization from files
   - `CifNormalizer`: strips a leading `RO` prefix and whitespace from a CIF/CUI before it is sent to ANAF

### Key Dependencies

- **Ardalis.GuardClauses**: Input validation and defensive programming
- **Microsoft.Extensions.Http**: HTTP client factory and lifecycle management (10.0.0)
- **UblSharp**: UBL 2.1 document parsing and manipulation
- **UblSharp.Validation**: UBL document validation capabilities
- **FluentValidation** / **FluentValidation.DependencyInjectionExtensions**: Comprehensive validation framework
- **System.Text.Json** (BCL, no package reference needed): all (de)serialization. `Newtonsoft.Json` and
  `Microsoft.Extensions.Hosting.Abstractions` are **not** dependencies of this library.

### ANAF API Endpoints

The authenticated (Bearer, OAuth2) endpoints resolve from `RoEFacturaOptions.Environment` (default
`Test`) or the `ApiBaseUrl` override. The public services always target production and never require
authentication.

**Authenticated endpoints (base URL depends on `RoEFactura:Environment`):**
- Test base: `https://api.anaf.ro/test/FCTEL/rest`
- Production base: `https://api.anaf.ro/prod/FCTEL/rest`
- Upload: `{base}/upload` (or `{base}/uploadb2c` for B2C), query `standard`, `cif`, optional
  `extern=DA`/`autofactura=DA`/`executare=DA`
- Status: `{base}/stareMesaj?id_incarcare={uploadIndex}`
- List (non-paged): `{base}/listaMesajeFactura?zile={1..60}&cif={cif}[&filtru={filter}]`
- List (paged): `{base}/listaMesajePaginatieFactura?startTime={ms}&endTime={ms}&cif={cif}&pagina={page}[&filtru={filter}]`
- Download: `{base}/descarcare?id={downloadId}`

**Public services (always production, stateless, no auth — independent of `Environment`):**
- Validate: `https://webservicesp.anaf.ro/prod/FCTEL/rest/validare/{FACT1|FCN}`
- XML→PDF: `https://webservicesp.anaf.ro/prod/FCTEL/rest/transformare/{FACT1|FCN}[/DA]`

**OAuth endpoints (defaults; overridable via `AnafOAuthOptions.AuthorizeUrl`/`TokenUrl`):**
- Authorize: `https://logincert.anaf.ro/anaf-oauth2/v1/authorize`
- Token: `https://logincert.anaf.ro/anaf-oauth2/v1/token`

### Authentication Flows

#### **Certificate-based Authentication (Desktop/Server Apps)**
1. Client certificate is loaded from Windows Certificate Store (CurrentUser/My)
2. Certificate-based OAuth2 flow with JWT tokens
3. Bearer token authentication for API calls
4. Automatic certificate discovery based on Romanian CA issuers

#### **OAuth Web Flow (Web Applications)**
1. Generate authorization URL with state parameter (CSRF protection)
2. User redirected to ANAF login page with digital certificate selection
3. Authorization code received via callback URL
4. Code exchanged for JWT access token using Basic authentication against `options.TokenUrl`
5. Bearer token used for subsequent API calls
6. `RefreshAccessTokenAsync(refreshToken, options, ct)` exchanges a refresh token for a new access
   token without a full re-authorization. The response may also contain a new refresh token; if it
   does not, `Token.RefreshToken` comes back empty and the caller must keep using the previous
   refresh token

### Romanian CIUS-RO 1.0.1 Validation

**Key validation rules implemented** (see [docs/VALIDATION_RULES.md](docs/VALIDATION_RULES.md) for the
authoritative, complete table with sources and code locations):
- **BR-RO-001**: CustomizationID (BT-24) must equal the CIUS-RO 1.0.1 identifier exactly
- **BR-RO-010**: Invoice number must contain at least one digit
- **BR-RO-020**: Invoice type code must be one of: 380, 389, 384, 381, 751
- **BR-RO-040**: VAT point date code, when present, must be one of 3, 35, 432
- **BR-RO-100/101**: Bucharest addresses (county `RO-B`) require a city of `SECTOR1`..`SECTOR6` (seller/buyer)
- **BR-RO-110/111**: Romanian addresses must use an ISO 3166-2:RO county code with the `RO-` prefix (seller/buyer)
- **BR-RO-120**: Romanian buyers must have a fiscal identification code (CUI/CIF)
- **BR-12/BR-13, BR-CO-10..17**: official EN 16931 totals formulas (BT-106/BT-109/BT-112/BT-115 and VAT breakdown)
- **BR-{S,Z,E,AE,IC,G,O}-05/-10**: VAT category rate and exemption-reason rules
- **Length limits**: `BR-RO-L100`/`L200`/`L300` (official field-length families)
- **County codes**: ISO 3166-2:RO validation for Romanian addresses (`RO-` prefix)

**Not validated locally** (documented in `docs/VALIDATION_RULES.md`, "Out of scope"): BR-RO-130
(depends on the `executare=DA` upload flag, not decidable from the XML alone), the full field-specific
`BR-RO-L*` catalogue, BR-CO-18/19, and UNTDID/UNECE code-list checks. `ValidateWithAnafAsync` remains
the final authority for anything not implemented locally.

**Validation features:**
- EN 16931 core + CIUS-RO 1.0.1 compliance checking
- Romanian address validation (including Bucharest sectors)
- VAT category and rate validation
- Monetary totals consistency checks
- Invoice line validation with UBL mapping
- Every validator predicate is null-safe: sparse/empty documents never throw

### Usage Patterns

#### **Basic Setup (Certificate-based, ANAF TEST by default)**
```csharp
// In Startup/Program.cs
services.AddRoEFactura(); // or services.AddRoEFactura(configuration) to bind the RoEFactura section

// Usage - Certificate authentication
var token = await anafOAuthClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
var invoices = await anafEInvoiceClient.ListEInvoicesAsync(token.AccessToken, days, cui, filter: null, ct);
```

#### **OAuth Web Flow Setup**
```csharp
// In Startup/Program.cs - Direct configuration
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = "your-client-id",
    ClientSecret = "your-client-secret",
    RedirectUri = "https://yourapp.com/oauth/callback"
});

// Or from configuration (also binds the RoEFactura section from the same IConfiguration)
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");

// Usage - OAuth web flow
var authUrl = anafOAuthClient.GenerateAuthorizationUrl(options, state);
// Redirect user to authUrl
// On callback:
var token = await anafOAuthClient.ExchangeAuthorizationCodeAsync(code, options, ct);
var invoices = await anafEInvoiceClient.ListEInvoicesAsync(token.AccessToken, days, cui, filter: null, ct);

// Refresh before expiry:
if (token.ExpiresAtUtc <= DateTimeOffset.UtcNow.AddMinutes(5))
{
    if (string.IsNullOrEmpty(token.RefreshToken))
    {
        // No refresh token on file (e.g. a prior refresh response omitted one) — re-authorize instead.
        return RedirectToAuthorize();
    }
    token = await anafOAuthClient.RefreshAccessTokenAsync(token.RefreshToken, options, ct);
}
```

#### **Upload and official-contract operations**
```csharp
var uploadResult = await anafEInvoiceClient.UploadAsync(
    token.AccessToken, xmlBytes, new AnafUploadOptions { Cif = cui }, ct);

var status = await anafEInvoiceClient.GetMessageStatusAsync(token.AccessToken, uploadResult.UploadIndex!, ct);

var anafValidation = await anafEInvoiceClient.ValidateWithAnafAsync(xmlBytes, AnafDocumentStandard.Ubl, ct);

byte[] pdf = await anafEInvoiceClient.ConvertToPdfAsync(xmlBytes, AnafDocumentStandard.Ubl, cancellationToken: ct);
```

#### **UBL Processing**
```csharp
// Process downloaded invoice with validation
var result = await anafEInvoiceClient.ProcessDownloadedInvoiceAsync(token.AccessToken, downloadId, ct);
if (result.IsSuccess)
{
    var invoice = result.Data; // UblSharp.InvoiceType
    var isRomanian = invoice.IsRomanianInvoice();
    var totalAmount = invoice.GetTotalAmountDue();
    var validationSummary = invoice.GetValidationSummary();
}
else
{
    // Handle validation errors
    foreach (var error in result.Errors)
    {
        _logger.LogWarning("{PropertyName}: {ErrorMessage}", error.PropertyName, error.ErrorMessage);
    }
}

// Batch processing
var downloadIds = new[] { "id1", "id2", "id3" };
var results = await anafEInvoiceClient.ProcessMultipleInvoicesAsync(token.AccessToken, downloadIds, ct);
```

#### **Custom Validation**
```csharp
// Local RO_CIUS/CIUS-RO validation (no ANAF call)
var xmlContent = File.ReadAllText("invoice.xml");
var validationResult = await anafEInvoiceClient.ValidateInvoiceXmlAsync(xmlContent);
if (!validationResult.IsSuccess)
{
    var romanianErrors = validationResult.Errors.Where(e => e.ErrorCode.StartsWith("BR-RO-"));
}
```

## micro-taxe compatibility

micro-taxe builds against this project via `-p:RoEFacturaProjectPath` and its tests use Moq expression
trees, so the following members must keep their **exact** declaration and behaviour (see the E1 plan,
§2.3, for the full rationale):

| Member | Constraint |
| --- | --- |
| `IAnafEInvoiceClient.ListPagedEInvoicesAsync(string, long, long, string, string filter = null, int page = 1)` | Kept verbatim, including `string filter = null` (not `string?`) |
| `IAnafEInvoiceClient.ProcessDownloadedInvoiceAsync(string, string)` | Kept verbatim |
| `IAnafEInvoiceClient.DownloadEInvoiceAsync(string, string, string, string)` | Kept, `[Obsolete]`, still writes to disk; all Guards run before any I/O |
| `IAnafEInvoiceClient.ListEInvoicesAsync(string, int, string, string filter = null)` | Kept verbatim |
| `IAnafOAuthClient.GenerateAuthorizationUrl(AnafOAuthOptions, string?)` | Signature and output unchanged |
| `IAnafOAuthClient.ExchangeAuthorizationCodeAsync(string, AnafOAuthOptions)` | Kept, delegates to the new CT overload |
| `Token.AccessToken`/`ExpiresIn`/`RefreshToken` | Unchanged JSON names; new members are `[JsonIgnore]` |
| `ServiceCollectionExtensions.AddRoEFacturaWithOAuth(IConfiguration, string sectionName = "AnafOAuth")` | Signature unchanged; now also binds `RoEFactura` |
| `RoCiusUblValidator` public parameterless constructor + `Validate` | Kept; must stay null-safe on sparse documents |
| `AnafDownloadErrorParser`, `AnafDownloadWindowExpiredException`, `ProcessingResult<T>` | Unchanged |
| DTO property names/types (`Items`, `PageCount`, etc.) | Unchanged; only additive properties (`Error`, `Title`) |

**Rule for changes to this library:** never add an optional parameter to an existing public member. New
capability is either a brand-new member, or a new overload of an existing member whose extra parameter
(typically `CancellationToken`) is **required**. Verify with the cross-repo build/test before merging:

```bash
dotnet build $MICROTAXE/Ram.MicroTaxes.sln -maxcpucount:1 -p:RoEFacturaProjectPath=$ROEFACTURA/RoEFactura/RoEFactura.csproj
dotnet test $MICROTAXE/Ram.MicroTaxes.sln --no-build -p:RoEFacturaProjectPath=$ROEFACTURA/RoEFactura/RoEFactura.csproj
```

## Important Notes

### **Authentication**
- Certificate selection is automatic based on Romanian CA issuer name patterns
- OAuth web flow includes CSRF protection via state parameters
- Token expiry (`Token.ExpiresAtUtc`) and refresh-token expiry (`Token.RefreshTokenExpiresAtUtc`,
  ~365 days per current ANAF policy) are computed from `IssuedAtUtc`
- Support for both hardcoded and configuration-based OAuth setup

### **Validation & Processing**
- EN 16931 core + CIUS-RO 1.0.1 compliance validation
- All Romanian-specific business rules (BR-RO-*) implemented are listed in `docs/VALIDATION_RULES.md`
- UBL 2.1 document processing with ZIP archive support, entirely in memory for the ANAF download path
- Processing statistics are per `UblProcessingService` instance (no static/shared state) and monitoring
- Batch processing for multiple invoices

### **Error Handling**
- All API methods include Guard clauses for input validation
- ANAF non-2xx responses throw `AnafApiException` (429 → `AnafRateLimitException`), both subclassing
  `HttpRequestException` so existing `catch (HttpRequestException)` handlers keep working
- Comprehensive validation results with specific error codes
- Romanian validation rules with detailed error messages that never interpolate document values
- The obsolete `DownloadEInvoiceAsync` still creates directories if they don't exist
- Descriptive exceptions with error context; no XML content, request bodies, party data, or full token
  values are ever logged (tokens are logged as a fingerprint only)

### **Development**
- **Tests:** `RoEFactura.Tests` (xUnit, FluentAssertions, Moq) — run `dotnet test RoEFactura.Tests/RoEFactura.Tests.csproj`. XML fixtures live under `RoEFactura.Tests/Fixtures/` as embedded resources (`ZipBuilder.LoadFixture`). No test calls the real ANAF network; HTTP is always faked (`RecordingHttpMessageHandler`/`OAuthRecordingHttpMessageHandler`).
- **UblSharp:** `InvoiceType.PayeeParty` and other aggregates may be non-null placeholder graphs after deserialization; `RoCiusUblValidator` only runs `PayeePartyValidator` when `IsPayeePartySpecified` detects real content. Assigning `null` to some element properties is ignored — use sentinel values where tests need “missing” data (e.g. `IssueDate` with `default` `DateTimeOffset`). Monetary/address “cleared” fields often deserialize as empty objects (`Value` 0, empty strings) rather than `null`. Amount presence is inferred from a non-empty `currencyID`, not from the amount being non-zero. `XmlSerializer` drops elements that are out of UBL schema order.
- FluentValidation integration for extensible validation rules
- Logging integration throughout the processing pipeline (`ILogger` only — no `Console.*` in `RoEFactura/`)
- Thread-safe, per-instance processing statistics tracking

### **Environment Considerations**
- The default is ANAF **TEST** (`https://api.anaf.ro/test/FCTEL/rest`); set `RoEFactura:Environment=Production`
  or `RoEFacturaOptions.Environment = AnafEnvironment.Production` to opt in explicitly.
- `ValidateWithAnafAsync`/`ConvertToPdfAsync` always target ANAF's public, stateless production services
  and never depend on `Environment`.
- `ApiBaseUrl`, when set, overrides `Environment` for the authenticated endpoints; it must be an
  absolute `http`/`https` URL.
- HTTP client factory pattern for proper connection management
- Romanian address validation including Bucharest sector handling
- Currency conversion rules for non-RON invoices (VAT accounting currency must be RON)
