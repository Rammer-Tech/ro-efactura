# RoEFactura API Reference

This document describes the public API surface of the `RoEFactura` NuGet package (v2.0.0). It is
organized by feature area and includes usage notes for each interface and model. For validation-rule
details, see [VALIDATION_RULES.md](VALIDATION_RULES.md); for migration from 1.x, see the "Migrating
from 1.x" section in the [README](../README.md).

## Dependency Injection

### `ServiceCollectionExtensions`

Register all services required by the library (ANAF **TEST** environment unless configured otherwise):

```csharp
services.AddRoEFactura();
// or, binding the "RoEFactura" configuration section (Environment, ApiBaseUrl, PublicServicesBaseUrl):
services.AddRoEFactura(configuration);
// or, programmatically:
services.AddRoEFactura(options => options.Environment = AnafEnvironment.Production);
```

Register with OAuth configuration:

```csharp
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth"); // also binds "RoEFactura" from the same configuration
// or
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = "...",
    ClientSecret = "...",
    RedirectUri = "https://example.com/oauth/callback"
});
```

Signatures:
- `AddRoEFactura(this IServiceCollection services, IConfiguration? configuration = null)`
- `AddRoEFactura(this IServiceCollection services, Action<RoEFacturaOptions> configure)`
- `AddRoEFacturaWithOAuth(this IServiceCollection services, AnafOAuthOptions oauthOptions, IConfiguration? configuration = null)`
- `AddRoEFacturaWithOAuth(this IServiceCollection services, IConfiguration configuration, string sectionName = "AnafOAuth")`

Notes:
- `AddRoEFactura(...)` registers `IAnafOAuthClient`, `IAnafEInvoiceClient` (as a typed `HttpClient`),
  and `IUblProcessingService`.
- `AddRoEFacturaWithOAuth(...)` validates `AnafOAuthOptions` before registering and throws
  `ArgumentException`/`InvalidOperationException` if invalid.
- A missing `RoEFactura` configuration section, a missing key, or an unset `Environment` value all
  resolve to `AnafEnvironment.Test`.

## Environment configuration

### `RoEFacturaOptions` (section `RoEFactura`)

| Property | Type | Default | Description |
| --- | --- | --- | --- |
| `Environment` | `AnafEnvironment` | `Test` | Selects the authenticated ANAF base URL |
| `ApiBaseUrl` | `string?` | `null` | Overrides `Environment` for authenticated endpoints; must be an absolute http(s) URL |
| `PublicServicesBaseUrl` | `string` | `https://webservicesp.anaf.ro/prod/FCTEL/rest` | Base URL for `ValidateWithAnafAsync`/`ConvertToPdfAsync` (always production, stateless) |

Methods:
- `string ResolveApiBaseUrl()` — resolves `ApiBaseUrl` or the environment's default; throws
  `InvalidOperationException` if `ApiBaseUrl` is set but not an absolute http(s) URL, or `Environment`
  is out of range.
- `string ResolvePublicServicesBaseUrl()` — same validation applied to `PublicServicesBaseUrl`.

Constants: `RoEFacturaOptions.SectionName` (`"RoEFactura"`), `TestApiBaseUrl`
(`https://api.anaf.ro/test/FCTEL/rest`), `ProductionApiBaseUrl` (`https://api.anaf.ro/prod/FCTEL/rest`),
`DefaultPublicServicesBaseUrl`.

### `AnafEnvironment`

```csharp
public enum AnafEnvironment { Test = 0, Production = 1 }
```

## Authentication

### `IAnafOAuthClient`

Certificate-based authentication (desktop/server):

```csharp
Token token = await anafOAuthClient.GetAccessTokenAsync(
    clientId: "...",
    clientSecret: "...",
    callbackUrl: "https://example.com/callback");
```

Overloads:
- `GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl)`
- `GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret, string callbackUrl)`
- `GetAccessTokenAsync(string thumbprint, string clientId, string clientSecret, string callbackUrl)`

Possible exceptions:
- `InvalidOperationException` when no valid certificates are found or multiple are available.

OAuth redirect flow (web apps), including refresh:

```csharp
string state = "..."; // CSRF token
string authUrl = anafOAuthClient.GenerateAuthorizationUrl(options, state);

// After callback:
Token token = await anafOAuthClient.ExchangeAuthorizationCodeAsync(code, options, cancellationToken);

// Before expiry:
Token refreshed = await anafOAuthClient.RefreshAccessTokenAsync(token.RefreshToken!, options, cancellationToken);
```

Methods:
- `string GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null)`
- `string GenerateAuthorizationUrl(AnafOAuthOptions options, string? state = null)`
- `Task<Token> ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri)`
- `Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options)`
- `Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options, CancellationToken cancellationToken)` *(new in 2.0.0)*
- `Task<Token> RefreshAccessTokenAsync(string refreshToken, AnafOAuthOptions options, CancellationToken cancellationToken = default)` *(new in 2.0.0)*

All members are unchanged from 1.x except the two marked "new in 2.0.0"; no optional parameter was
added to any existing member.

### `Token`

| Property | Type | Description |
| --- | --- | --- |
| `AccessToken` | `string` | JWT access token (`access_token`) |
| `RefreshToken` | `string` | Refresh token, if provided (`refresh_token`) |
| `ExpiresIn` | `int` | Expiration in seconds (`expires_in`) |
| `TokenType` | `string` | Typically `Bearer` (`token_type`) |
| `Scope` | `string` | Scope, if provided (`scope`) |
| `IssuedAtUtc` | `DateTimeOffset?` | *(new, `[JsonIgnore]`)* Set by the client when the token response is parsed |
| `ExpiresAtUtc` | `DateTimeOffset?` | *(new, `[JsonIgnore]`, computed)* `IssuedAtUtc + ExpiresIn` seconds |
| `RefreshTokenExpiresAtUtc` | `DateTimeOffset?` | *(new, `[JsonIgnore]`, computed)* `IssuedAtUtc + RefreshTokenLifetime`, or `null` when `RefreshToken` is empty |
| `RefreshTokenLifetime` | `static TimeSpan` | 365 days, per current ANAF OAuth policy (subject to change without notice) |

### `TokenExchangeException`

`ExchangeAuthorizationCodeAsync`/`RefreshAccessTokenAsync` can throw `TokenExchangeException` with:
- `ErrorType` (`TokenExchangeErrorType`): `NetworkError`, `Timeout`, `AuthenticationFailed`,
  `InvalidRequest`, `InvalidResponse`, `RateLimited`, `ServiceUnavailable`, `ServerError`,
  `UnknownError`, `InvalidGrant` *(new in 2.0.0 — authorization code or refresh token invalid, expired
  or revoked; re-run the OAuth authorization flow from the start)*.
- `StatusCode` if available.
- `ServerResponse` with raw response content if available (never populated for a success body that may
  contain live tokens; only for error responses).

## E-Invoice API Client

### `IAnafEInvoiceClient`

Every member below accepts a `CancellationToken` (required on the new overloads of members kept from
1.x, optional — defaulting to `default` — on brand-new members). Unless noted, calls target the
authenticated base URL resolved from `RoEFacturaOptions` and send `Authorization: Bearer {token}`.

#### Upload

```csharp
AnafUploadResult result = await anafEInvoiceClient.UploadAsync(
    token.AccessToken, xmlBytes, new AnafUploadOptions { Cif = "12345678" }, cancellationToken);
```

`Task<AnafUploadResult> UploadAsync(string token, byte[] xml, AnafUploadOptions options, CancellationToken cancellationToken = default)`

- Posts to `{base}/upload`, or `{base}/uploadb2c` when `options.IsB2C`.
- Query: `standard` (`UBL`/`CN`/`CII`/`RASP`, from `options.Standard`), `cif` (normalized via
  `CifNormalizer`), optional `extern=DA` (`options.ExternalBuyer`), `autofactura=DA`
  (`options.SelfBilling`), `executare=DA` (`options.Enforcement`).
- Body: raw XML bytes, `Content-Type: text/plain`. Max 10 MB (`ArgumentException` before any HTTP call).
- Throws `AnafApiException`/`AnafRateLimitException` on non-2xx; `ArgumentException`/`ArgumentNullException` on bad input.

`AnafUploadOptions` (`init`-only):

| Property | Type | Default | Query effect |
| --- | --- | --- | --- |
| `Standard` | `AnafDocumentStandard` | `Ubl` | `standard` |
| `Cif` | `required string` | — | `cif` (normalized) |
| `IsB2C` | `bool` | `false` | selects `uploadb2c` path |
| `ExternalBuyer` | `bool` | `false` | `extern=DA` |
| `SelfBilling` | `bool` | `false` | `autofactura=DA` |
| `Enforcement` | `bool` | `false` | `executare=DA` |

`AnafUploadResult` (`init`-only): `bool IsSuccess`, `string? UploadIndex`, `DateTimeOffset? ResponseDate`,
`IReadOnlyList<string> Errors` (never null), `required string RawResponse`.

`AnafDocumentStandard`: `Ubl` (query `UBL`, validate/pdf `FACT1`), `CreditNote` (query `CN`, validate/pdf
`FCN`), `Cii` (query `CII`, not accepted by validate/pdf), `Rasp` (query `RASP`, not accepted by
validate/pdf). `ValidateWithAnafAsync`/`ConvertToPdfAsync` throw `ArgumentOutOfRangeException` for `Cii`/`Rasp`.

#### Status (`stareMesaj`)

```csharp
AnafMessageStatusResult status = await anafEInvoiceClient.GetMessageStatusAsync(
    token.AccessToken, uploadIndex, cancellationToken);
```

`Task<AnafMessageStatusResult> GetMessageStatusAsync(string token, string uploadIndex, CancellationToken cancellationToken = default)`

`AnafMessageStatusResult` (`init`-only): `AnafMessageState State`, `string? DownloadId`,
`IReadOnlyList<string> Errors`, `required string RawResponse`.

`AnafMessageState`: `InProcessing` (`in prelucrare`), `Ok` (`ok`), `Nok` (`nok`), `RejectedAtUpload`
(`XML cu erori nepreluat de sistem`), `Unknown` (missing/unrecognized `stare`).

#### Lists (non-paged and paged)

```csharp
List<EInvoiceAnafResponse> items = await anafEInvoiceClient.ListEInvoicesAsync(
    token.AccessToken, days: 30, cui: "RO12345678", filter: null, cancellationToken);

EInvoiceAnafPagedListResponse page = await anafEInvoiceClient.ListPagedEInvoicesAsync(
    token.AccessToken, startMs, endMs, "RO12345678", filter: null, page: 1, cancellationToken);
```

Methods (1.x members kept **verbatim**, each with a new `CancellationToken`-required overload):
- `Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string filter = null)`
- `Task<List<EInvoiceAnafResponse>> ListEInvoicesAsync(string token, int days, string cui, string? filter, CancellationToken cancellationToken)`
- `Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(string token, long startMilliseconds, long endMilliseconds, string cui, string filter = null, int page = 1)`
- `Task<EInvoiceAnafPagedListResponse> ListPagedEInvoicesAsync(string token, long startMilliseconds, long endMilliseconds, string cui, string? filter, int page, CancellationToken cancellationToken)`

`days` must be between 1 and 60 (`ArgumentException` if ≤ 0, `ArgumentOutOfRangeException` if > 60).
A JSON `eroare` response returns an empty list, or empty `Items` with `Error`/`Title` set, instead of
throwing. `cui` is normalized via `CifNormalizer` (accepts an optional `RO` prefix and whitespace).

Notes on `filter`:
- Passed through as ANAF query parameter `filtru`. Accepted values: `E` (ERORI FACTURA), `T` (FACTURA
  TRIMISA), `P` (FACTURA PRIMITA), `R` (MESAJ CUMPARATOR). Not validated by this library.

#### Download

```csharp
AnafDownloadResult download = await anafEInvoiceClient.DownloadMessageAsync(
    token.AccessToken, downloadId, cancellationToken);
```

`Task<AnafDownloadResult> DownloadMessageAsync(string token, string downloadId, CancellationToken cancellationToken = default)`

Downloads and splits the ANAF ZIP **entirely in memory**: `DocumentXml`/`DocumentFileName` is the
invoice/error entry, `SignatureXml`/`SignatureFileName` is the Ministry of Finance signature sidecar
(identified via `EInvoiceXmlFileFilter.IsSemnaturaXmlFileName`). Throws
`AnafDownloadWindowExpiredException` when ANAF reports the 60-day window has passed, or `AnafApiException`
for any other non-ZIP/error response.

`AnafDownloadResult` (`init`-only): `required byte[] ZipContent`, `required string FileName`,
`byte[]? DocumentXml`, `string? DocumentFileName`, `byte[]? SignatureXml`, `string? SignatureFileName`.

Deprecated, disk-based equivalent (kept for 1.x compatibility):

```csharp
[Obsolete("Use DownloadMessageAsync; this member writes to disk.")]
Task DownloadEInvoiceAsync(string token, string zipDestinationPath, string unzipDestinationPath, string eInvoiceDownloadId);
```

All 4 Guards run before any I/O (an empty `eInvoiceDownloadId` throws `ArgumentException` before any
directory is created). Internally delegates to `DownloadMessageAsync`.

#### Process downloaded invoices (local UBL parsing, ANAF validation skipped)

```csharp
ProcessingResult<InvoiceType> result =
    await anafEInvoiceClient.ProcessDownloadedInvoiceAsync(token.AccessToken, "download_id", cancellationToken);
```

- `Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId)`
- `Task<ProcessingResult<InvoiceType>> ProcessDownloadedInvoiceAsync(string token, string eInvoiceDownloadId, CancellationToken cancellationToken)`

Downloads via `DownloadMessageAsync`, then parses `DocumentXml` with
`IUblProcessingService.ProcessInvoiceXmlAsync(bytes, id, skipValidation: true)` (RO_CIUS validation is
skipped because SPV documents were already accepted by ANAF at upload time). A ZIP with only a
signature sidecar returns `ProcessingResult.Failed(...)`. `AnafDownloadWindowExpiredException` rethrows;
other exceptions return `ProcessingResult.Failed($"Processing error: {message}")`.

Batch:
- `Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(string token, IEnumerable<string> eInvoiceDownloadIds)`
- `Task<List<ProcessingResult<InvoiceType>>> ProcessMultipleInvoicesAsync(string token, IEnumerable<string> eInvoiceDownloadIds, CancellationToken cancellationToken)`

#### Local validation (RO_CIUS/CIUS-RO, no ANAF call)

```csharp
ProcessingResult<InvoiceType> localValidation =
    await anafEInvoiceClient.ValidateInvoiceXmlAsync(xmlContent);
```

`Task<ProcessingResult<InvoiceType>> ValidateInvoiceXmlAsync(string xmlContent)` — unchanged from 1.x.

#### Validate with ANAF (public, stateless)

```csharp
AnafValidationResult validation = await anafEInvoiceClient.ValidateWithAnafAsync(
    xmlBytes, AnafDocumentStandard.Ubl, cancellationToken);
```

`Task<AnafValidationResult> ValidateWithAnafAsync(byte[] xml, AnafDocumentStandard standard, CancellationToken cancellationToken = default)`

Posts raw XML (`Content-Type: text/plain`) to `{PublicServicesBaseUrl}/validare/{FACT1|FCN}`. **Never
sends an Authorization header**, and always targets `PublicServicesBaseUrl` (production, stateless)
regardless of the configured `Environment`. Max 5 MB. `standard` must be `Ubl` or `CreditNote`
(`ArgumentOutOfRangeException` otherwise).

`AnafValidationResult` (`init`-only): `bool IsValid`, `IReadOnlyList<string> Messages`, `string? TraceId`,
`required string RawResponse`.

#### XML → PDF

```csharp
byte[] pdfBytes = await anafEInvoiceClient.ConvertToPdfAsync(xmlBytes, AnafDocumentStandard.Ubl, cancellationToken: cancellationToken);
```

`Task<byte[]> ConvertToPdfAsync(byte[] xml, AnafDocumentStandard standard, bool validate = true, CancellationToken cancellationToken = default)`

Posts to `{PublicServicesBaseUrl}/transformare/{FACT1|FCN}` (appends `/DA` when `validate == false`).
Same auth/environment/size rules as `ValidateWithAnafAsync`. On a non-PDF (JSON) response with
validation failures, throws `AnafApiException` with `Errors` populated from the parsed messages.

#### Removed in 2.0.0

`ValidateXmlAsync`, `ValidateXmlContentAsync`, `UploadXmlAsync`, `UploadXmlContentAsync` were removed.
Use `ValidateWithAnafAsync`/`UploadAsync` instead (see "Migrating from 1.x" in the README).

## UBL Processing

### `IUblProcessingService`

This service processes UBL data locally (no ANAF API call). Statistics are tracked **per service
instance** (no static/shared state).

Methods:
- `Task<ProcessingResult<InvoiceType>> ProcessInvoiceXmlAsync(byte[] xmlData, string fileName, bool skipValidation = false)`
- `Task<ProcessingResult<InvoiceType>> ProcessInvoiceZipAsync(byte[] zipData, string fileName)`
- `Task<ProcessingResult<InvoiceType>> ValidateInvoiceAsync(InvoiceType invoice)`
- `ProcessingStats GetProcessingStats()`
- `void ResetProcessingStats()`

`ProcessInvoiceXmlAsync` decodes `xmlData` BOM-safely (UTF-8 with or without a byte-order mark).

### `ProcessingResult<T>`

Result wrapper for processing and validation:

| Property | Type | Description |
| --- | --- | --- |
| `IsSuccess` | `bool` | `true` when validation succeeded |
| `Data` | `T?` | Parsed data when success |
| `Errors` | `List<ValidationFailure>` | Validation errors |
| `Warnings` | `List<string>` | Optional warnings |

Factory methods:
- `ProcessingResult<T>.Success(T data)`
- `ProcessingResult<T>.Failed(IEnumerable<ValidationFailure> errors)`
- `ProcessingResult<T>.Failed(string errorMessage)`
- `WithWarnings(IEnumerable<string> warnings)`

### `ProcessingStats`

Statistics from `GetProcessingStats()`:

| Property | Type | Description |
| --- | --- | --- |
| `TotalProcessed` | `int` | Total invoices processed |
| `SuccessfullyProcessed` | `int` | Successful validations |
| `ValidationErrors` | `int` | Validation error count |
| `ProcessingErrors` | `int` | Processing error count |
| `LastProcessedAt` | `DateTime?` | Last processing timestamp |
| `SuccessRate` | `double` | Success rate (0-100) |

Thread safety:
- Statistics are updated using a per-instance lock and are safe to read concurrently. Two
  `UblProcessingService` instances never share counters.

## Error Types

| Type | Base type | Key members |
| --- | --- | --- |
| `AnafApiException` | `HttpRequestException` | `RawResponse`, `Errors`, `IsUnauthorized` (401/403) |
| `AnafRateLimitException` | `AnafApiException` | `RetryAfter` (`TimeSpan?`, from the `Retry-After` header) |
| `AnafDownloadWindowExpiredException` | `Exception` | `AnafDownloadId`, `AnafErrorMessage` |
| `TokenExchangeException` | `Exception` | `ErrorType` (`TokenExchangeErrorType`), `StatusCode`, `ServerResponse` |

`AnafApiException`/`AnafRateLimitException` are thrown for every non-2xx response from every
`IAnafEInvoiceClient` member, list calls included; existing `catch (HttpRequestException)` handlers
keep working because both are subclasses of it. Transport failures (DNS, connection refused) still
surface as a plain `HttpRequestException`.

## Models and DTOs

### OAuth Models

`AnafOAuthOptions`

| Property | Type | Description |
| --- | --- | --- |
| `ClientId` | `string` | ANAF OAuth client id |
| `ClientSecret` | `string` | ANAF OAuth client secret |
| `RedirectUri` | `string` | Registered callback URL |
| `AuthorizeUrl` | `string` | Default `https://logincert.anaf.ro/anaf-oauth2/v1/authorize` (`AnafOAuthOptions.DefaultAuthorizeUrl`) |
| `TokenUrl` | `string` | Default `https://logincert.anaf.ro/anaf-oauth2/v1/token` (`AnafOAuthOptions.DefaultTokenUrl`) |
| `IncludeTokenContentType` | `bool` | Include `token_content_type=jwt` |
| `Prompt` | `string?` | OAuth `prompt` query parameter; default `"login"` |
| `Nonce` | `string?` | OpenID Connect `nonce`; default `null` |

Other OAuth models:
- `OAuthInitiateResponse` (Success, AuthorizationUrl, State, Error)
- `OAuthTokenResponse` (AccessToken, RefreshToken, ExpiresIn, TokenType, Scope)
- `OAuthAuthorizationStatus` (IsAuthorized, ExpiresAt, TokenType, ExpiresIn, AdditionalInfo)
- `OAuthCodeExchangeRequest` (Code, State, RedirectUri)

### ANAF Response DTOs

`EInvoiceAnafResponse`

| Property | Type | Description |
| --- | --- | --- |
| `CreatedAt` | `string` | Creation timestamp from ANAF |
| `Cif` | `string` | Fiscal code |
| `RequestId` | `string` | Request id |
| `Details` | `string` | Details or status |
| `Type` | `string` | Message type |
| `Id` | `string` | Invoice identifier |

`ListEInvoicesAnafResponse`

| Property | Type | Description |
| --- | --- | --- |
| `Items` | `List<EInvoiceAnafResponse>` | Non-paged list payload (initialized to `[]`) |
| `Error` | `string?` | *(new)* Set from the `eroare` field when ANAF returns a JSON error instead of a list |
| `Title` | `string?` | *(new)* Set from the `titlu` field alongside `Error` |

`EInvoiceAnafPagedListResponse`

| Property | Type | Description |
| --- | --- | --- |
| `Items` | `List<EInvoiceAnafResponse>` | Page items (initialized to `[]`) |
| `CurrentPageCount` | `int` | Items in current page |
| `MaxPageCount` | `int` | Items per page |
| `TotalItemCount` | `int` | Total items |
| `PageCount` | `int` | Total pages |
| `CurrentPageIndex` | `int` | Current page index |
| `Serial` | `string` | ANAF serial |
| `Cui` | `string` | CUI for the query |
| `Title` | `string` | Response title |
| `Error` | `string?` | *(new)* Set from the `eroare` field when ANAF returns a JSON error instead of a page |

No property was renamed or retyped from 1.x; `Error`/`Title` are additive.

### CertificateInfo

`CertificateInfo` holds certificate metadata (subject, issuer, thumbprint, expiry). It is used internally for certificate discovery and validation.

## Utilities

### `CifNormalizer`

```csharp
string normalized = CifNormalizer.Normalize("RO 12345678"); // "12345678"
```

`public static string Normalize(string cif)` — trims whitespace, strips a leading `RO` prefix
(case-insensitive), removes remaining whitespace. Throws `ArgumentException` if the result is empty or
contains non-digit characters. Used internally by every `IAnafEInvoiceClient` member that accepts a
`cui`/`cif`.

## Extension Methods

### `InvoiceTypeExtensions`
- `IsRomanianInvoice()` — true for the current CIUS-RO 1.0.1 id, the legacy RO_CIUS 1.0.0.2021 id, or RO parties
- `GetCurrencyCode()`
- `GetTotalAmountDue()`
- `GetTotalWithoutVat()`
- `GetTotalWithVat()`
- `GetTotalVat()`
- `GetSumOfLineNet()`
- `GetTaxInclusiveAmount()` / `GetTaxExclusiveAmount()`
- `GetPrecedingInvoiceId()` / `GetPrecedingInvoiceIssueDate()`
- `GetValidationSummary()`

### `PartyExtensions`
- `GetSellerVatId()`, `GetBuyerVatId()`
- `GetSellerLegalId()`, `GetBuyerLegalId()`
- `GetSellerName()`, `GetBuyerName()`
- `GetSellerCountryCode()`, `GetBuyerCountryCode()`
- `GetPayeeName()`, `GetPayeeVatId()`, `GetPayeeLegalId()`

### `UblSharpExtensions`
- `LoadInvoiceFromXml(string xmlContent)`
- `SaveInvoiceToXml(this InvoiceType invoice)` — serializes to a string; the XML declaration correctly
  reads `encoding="utf-8"`
- `SaveInvoiceToXmlBytes(this InvoiceType invoice)` *(new in 2.0.0)* — serializes to UTF-8 bytes
  **without** a byte-order mark

## Notes

- The default ANAF environment is **TEST**; production requires explicit opt-in via
  `RoEFactura:Environment=Production` or `RoEFacturaOptions.Environment`.
- `ValidateWithAnafAsync`/`ConvertToPdfAsync` are public, stateless ANAF services: they never require
  authentication and always target production, regardless of `Environment`.
- `ValidateInvoiceXmlAsync` performs local CIUS-RO 1.0.1 validation, not an ANAF API call.
- `UploadAsync`/`ValidateWithAnafAsync`/`ConvertToPdfAsync` send the raw XML as
  `Content-Type: text/plain`, per the official ANAF contract (not multipart form data).
