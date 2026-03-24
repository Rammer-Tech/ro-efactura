# RoEFactura API Reference

This document describes the public API surface of the `RoEFactura` NuGet package. It is organized by feature area and includes usage notes for each interface and model.

## Dependency Injection

### `ServiceCollectionExtensions`

Register all services required by the library:

```csharp
services.AddRoEFactura();
```

Register with OAuth configuration:

```csharp
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");
// or
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = "...",
    ClientSecret = "...",
    RedirectUri = "https://example.com/oauth/callback"
});
```

Notes:
- `AddRoEFactura()` registers `IAnafOAuthClient`, `IAnafEInvoiceClient`, and `IUblProcessingService`.
- `AddRoEFacturaWithOAuth(...)` validates `AnafOAuthOptions` before registering.

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

OAuth redirect flow (web apps):

```csharp
string state = "..."; // CSRF token
string authUrl = anafOAuthClient.GenerateAuthorizationUrl(options, state);

// After callback:
Token token = await anafOAuthClient.ExchangeAuthorizationCodeAsync(code, options);
```

Methods:
- `GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null)`
- `GenerateAuthorizationUrl(AnafOAuthOptions options, string? state = null)`
- `ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri)`
- `ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options)`

### `TokenExchangeException`

`ExchangeAuthorizationCodeAsync` can throw `TokenExchangeException` with:
- `ErrorType` (`TokenExchangeErrorType`) such as `NetworkError`, `Timeout`, `AuthenticationFailed`, `InvalidRequest`, `InvalidResponse`, `RateLimited`, `ServiceUnavailable`, `ServerError`.
- `StatusCode` if available.
- `ServerResponse` with raw response content if available.

## E-Invoice API Client

### `IAnafEInvoiceClient`

List invoices (non-paged):

```csharp
List<EInvoiceAnafResponse> items = await anafEInvoiceClient.ListEInvoicesAsync(
    token.AccessToken, days: 30, cui: "RO12345678", filter: null);
```

List invoices (paged):

```csharp
long start = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
long end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

EInvoiceAnafPagedListResponse page = await anafEInvoiceClient.ListPagedEInvoicesAsync(
    token.AccessToken, start, end, "RO12345678", filter: null, page: 1);
```

Methods:
- `ListEInvoicesAsync(string token, int days, string cui, string filter = null)`
- `ListPagedEInvoicesAsync(string token, long startMilliseconds, long endMilliseconds, string cui, string filter = null, int page = 1)`

Notes on `filter`:
- Passed through as ANAF query parameter `filtru`.
- Accepted values are defined by ANAF and may change; consult official ANAF eFactura API documentation for your environment.

Download an invoice as ZIP:

```csharp
await anafEInvoiceClient.DownloadEInvoiceAsync(
    token.AccessToken,
    zipDestinationPath: "/tmp/efactura/zip",
    unzipDestinationPath: "/tmp/efactura/extracted",
    eInvoiceDownloadId: "download_id");
```

Validate XML using ANAF API:

```csharp
string result = await anafEInvoiceClient.ValidateXmlAsync(
    token.AccessToken, "/absolute/path/invoice.xml");

string result2 = await anafEInvoiceClient.ValidateXmlContentAsync(
    token.AccessToken, xmlContent, "invoice.xml");
```

Upload XML using ANAF API:

```csharp
string uploadResult = await anafEInvoiceClient.UploadXmlAsync(
    token.AccessToken, "/absolute/path/invoice.xml");

string uploadResult2 = await anafEInvoiceClient.UploadXmlContentAsync(
    token.AccessToken, xmlContent, "invoice.xml");
```

Process downloaded invoices locally (RO_CIUS):

```csharp
ProcessingResult<InvoiceType> result =
    await anafEInvoiceClient.ProcessDownloadedInvoiceAsync(token.AccessToken, "download_id");

ProcessingResult<InvoiceType> localValidation =
    await anafEInvoiceClient.ValidateInvoiceXmlAsync(xmlContent);
```

Batch processing:

```csharp
var results = await anafEInvoiceClient.ProcessMultipleInvoicesAsync(
    token.AccessToken, new[] { "id1", "id2" });
```

## UBL Processing

### `IUblProcessingService`

This service processes UBL data locally (no ANAF API call).

Methods:
- `ProcessInvoiceXmlAsync(byte[] xmlData, string fileName)`
- `ProcessInvoiceZipAsync(byte[] zipData, string fileName)`
- `ValidateInvoiceAsync(InvoiceType invoice)`
- `GetProcessingStats()`
- `ResetProcessingStats()`

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
- Statistics are updated using a lock and are safe to read concurrently.

## Models and DTOs

### OAuth Models

`AnafOAuthOptions`

| Property | Type | Description |
| --- | --- | --- |
| `ClientId` | `string` | ANAF OAuth client id |
| `ClientSecret` | `string` | ANAF OAuth client secret |
| `RedirectUri` | `string` | Registered callback URL |
| `AuthorizeUrl` | `string` | Default `https://logincert.anaf.ro/anaf-oauth2/v1/authorize` |
| `TokenUrl` | `string` | Default `https://logincert.anaf.ro/anaf-oauth2/v1/token` |
| `IncludeTokenContentType` | `bool` | Include `token_content_type=jwt` |

`Token`

| Property | Type | Description |
| --- | --- | --- |
| `AccessToken` | `string` | JWT access token |
| `RefreshToken` | `string` | Refresh token (if provided) |
| `ExpiresIn` | `int` | Expiration in seconds |
| `TokenType` | `string` | Typically `Bearer` |
| `Scope` | `string` | Scope (if provided) |

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
| `Items` | `List<EInvoiceAnafResponse>` | Non-paged list payload |

`EInvoiceAnafPagedListResponse`

| Property | Type | Description |
| --- | --- | --- |
| `Items` | `List<EInvoiceAnafResponse>` | Page items |
| `CurrentPageCount` | `int` | Items in current page |
| `MaxPageCount` | `int` | Items per page |
| `TotalItemCount` | `int` | Total items |
| `PageCount` | `int` | Total pages |
| `CurrentPageIndex` | `int` | Current page index |
| `Serial` | `string` | ANAF serial |
| `Cui` | `string` | CUI for the query |
| `Title` | `string` | Response title |

### CertificateInfo

`CertificateInfo` holds certificate metadata (subject, issuer, thumbprint, expiry). It is used internally for certificate discovery and validation.

## Extension Methods

### `InvoiceTypeExtensions`
- `IsRomanianInvoice()`
- `GetCurrencyCode()`
- `GetTotalAmountDue()`
- `GetTotalWithoutVat()`
- `GetTotalWithVat()`
- `GetTotalVat()`
- `GetSumOfLineNet()`
- `GetValidationSummary()`

### `PartyExtensions`
- `GetSellerVatId()`, `GetBuyerVatId()`
- `GetSellerLegalId()`, `GetBuyerLegalId()`
- `GetSellerName()`, `GetBuyerName()`
- `GetSellerCountryCode()`, `GetBuyerCountryCode()`
- `GetPayeeName()`, `GetPayeeVatId()`, `GetPayeeLegalId()`

### `UblSharpExtensions`
- `LoadInvoiceFromXml(string xmlContent)`
- `SaveInvoiceToXml(this InvoiceType invoice)`

## Notes

- ANAF endpoints are currently configured to production URLs in `AnafEInvoiceClient`.
- `ValidateInvoiceXmlAsync` performs local RO_CIUS validation, not an ANAF API call.
- XML file upload and validation use multipart form data with content type `application/xml`.
