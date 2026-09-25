# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog and this project follows Semantic Versioning.

## [Unreleased]

## [2.0.0] - 2026-09-25

Implements the official ANAF e-Factura contract (upload/uploadb2c, `stareMesaj`, list/paged list,
`descarcare`, public `validare`/`transformare`), a TEST-by-default environment, OAuth refresh-token
support, and a repaired CIUS-RO 1.0.1 local validator. See `docs/VALIDATION_RULES.md` for the full
validation-rule rewrite and `docs/API_REFERENCE.md` for the complete public API surface.

### Breaking changes

- **The default ANAF environment is now TEST** (`https://api.anaf.ro/test/FCTEL/rest`). Set
  `RoEFactura:Environment=Production` (or `RoEFacturaOptions.Environment = AnafEnvironment.Production`)
  to opt in to production. Previously the client was hardcoded to production endpoints.
- **Removed** `IAnafEInvoiceClient.ValidateXmlAsync`, `ValidateXmlContentAsync`, `UploadXmlAsync`, and
  `UploadXmlContentAsync`, and the `AnafEInvoiceClient` `IHostEnvironment` constructor and 5-string
  constructor. Use `ValidateWithAnafAsync`/`UploadAsync` instead (see "Added" below).
- **`Newtonsoft.Json` and `Microsoft.Extensions.Hosting.Abstractions` package references removed.**
  All JSON serialization now goes through `System.Text.Json`; `[JsonProperty]` attributes were replaced
  with `[JsonPropertyName]`.
- **Target framework is now `net10.0` only** (multi-targeting `net9.0`/`net10.0` was dropped).
- **The local validator is stricter and re-labeled to match the official CIUS-RO 1.0.1 schematron:**
  the customization ID constant now points at `CIUS-RO:1.0.1` (was `RO_CIUS:1.0.0.2021`, kept as the
  obsolete `RomanianConstants.RoCiusCustomizationId` for detection only); `RomanianConstants.ValidCountyCodes`
  values now carry the `RO-` prefix (e.g. `RO-CJ` instead of `CJ`); Bucharest city codes must be exact
  `SECTOR1`..`SECTOR6`; BR-CO-10..17 use the official formulas (BR-12/BR-13 swap meaning to match BT-106/BT-109);
  new VAT-category and length rules were added; the `BR-RO-CIUS` and `RO-LINE-NOTE-LENGTH`/`RO-ITEM-NAME-LENGTH`/
  `RO-ITEM-DESC-LENGTH` codes are gone in favor of `BR-RO-001`/`BR-RO-L100`/`BR-RO-L200`/`BR-RO-L300`.
- **List calls now throw `AnafApiException`/`AnafRateLimitException`** (both subclasses of
  `HttpRequestException`) instead of a plain `HttpRequestException` on non-2xx responses.
- **`IAnafEInvoiceClient.DownloadEInvoiceAsync` is now `[Obsolete]`** in favor of the in-memory
  `DownloadMessageAsync`; it still writes to disk exactly as before for compatibility.

### Added

- `UploadAsync`/`GetMessageStatusAsync`/`DownloadMessageAsync`/`ValidateWithAnafAsync`/`ConvertToPdfAsync` on
  `IAnafEInvoiceClient`: the official `upload`/`uploadb2c`, `stareMesaj`, `descarcare`, public `validare` and
  `transformare` operations, with typed options/results (`AnafUploadOptions`, `AnafUploadResult`,
  `AnafMessageStatusResult`, `AnafDownloadResult`, `AnafValidationResult`, `AnafDocumentStandard`).
- `RoEFacturaOptions` (section `RoEFactura`): `Environment` (`AnafEnvironment.Test`/`Production`),
  `ApiBaseUrl` override, `PublicServicesBaseUrl` (always production, stateless).
- `AnafApiException` (`: HttpRequestException`, with `RawResponse`/`Errors`/`IsUnauthorized`) and
  `AnafRateLimitException` (`: AnafApiException`, with `RetryAfter` parsed from the ANAF 429 response).
- `IAnafOAuthClient.RefreshAccessTokenAsync(string refreshToken, AnafOAuthOptions options, CancellationToken)`
  and a `CancellationToken`-overload of `ExchangeAuthorizationCodeAsync(string, AnafOAuthOptions, CancellationToken)`.
- `TokenExchangeErrorType.InvalidGrant` (authorization code or refresh token invalid, expired or revoked).
- `Token.IssuedAtUtc`/`ExpiresAtUtc`/`RefreshTokenExpiresAtUtc` and `Token.RefreshTokenLifetime` (365 days,
  per current ANAF OAuth policy).
- `CancellationToken`-required overloads of the existing `ListEInvoicesAsync`, `ListPagedEInvoicesAsync`,
  `ProcessDownloadedInvoiceAsync` and `ProcessMultipleInvoicesAsync` members. (The existing
  `DownloadEInvoiceAsync` is `[Obsolete]` instead of gaining a `CancellationToken` overload — use the new
  `DownloadMessageAsync`; the local, non-ANAF `ValidateInvoiceXmlAsync` was not given one either.)
- `CifNormalizer.Normalize`: strips a leading `RO` prefix and whitespace from a CIF/CUI.
- `UblSharpExtensions.SaveInvoiceToXmlBytes`: serializes to UTF-8 bytes without a BOM.
- `Validation.Constants.RoCiusRuleIds`: centralized CIUS-RO/EN 16931 rule-id constants.
- `VatBreakdownValidator` (BR-E-10/BR-AE-10/BR-IC-10/BR-G-10/BR-O-10) and new line-level VAT-category rate
  rules (BR-S-05/BR-Z-05/BR-E-05/BR-AE-05/BR-IC-05/BR-G-05/BR-O-05).
- `AddRoEFactura(this IServiceCollection, Action<RoEFacturaOptions> configure)` overload.

### Fixed

- Upload and validate/PDF now target the correct ANAF endpoints and raw `text/plain` request bodies.
- `eroare` list responses no longer silently become an empty result for every ANAF error.
  `ListPagedEInvoicesAsync` still returns `Items = []` with `Error`/`Title` set for any `eroare` (paged
  callers, including micro-taxe's sync job, must keep reading `Error`). `ListEInvoicesAsync` now returns
  an empty list **only** for ANAF's "no messages in this interval" `eroare` (`Nu exista mesaje...`); any
  other `eroare` (e.g. no query right for the CIF, an invalid CIF) throws `AnafApiException` carrying the
  ANAF message, instead of being indistinguishable from "no invoices".
- CIF/CUI values with a leading `RO` prefix or stray whitespace are now normalized before every ANAF call.
- `SaveInvoiceToXml` now correctly declares `encoding="utf-8"` in the XML prolog. It previously declared
  `encoding="utf-16"` regardless of `XmlWriterSettings.Encoding`, because `XmlWriter` takes the prolog's
  encoding from the `StringWriter`'s own `Encoding` (UTF-16) when writing to a `TextWriter`, not from
  the settings.
- `UblProcessingService` processing statistics are now per-instance instead of static, so concurrent
  service instances no longer share counters.
- Removed all `Console.*` output from the library; only `ILogger` is used.
- Logging no longer includes XML content, request bodies, party names/addresses, or full token values;
  tokens are logged as a fingerprint only.
- `AnafOAuthClient` now posts to `options.TokenUrl` (previously some flows silently used the hardcoded
  default token URL even when a custom one was configured).
- BR-RO-040 (VAT point date code) now actually checks `InvoicePeriod[*].DescriptionCode[*]` instead of a
  no-op condition on `TaxPointDate`.
- BR-RO-130 (payee enforcement) is no longer emitted, since it depends on the `executare=DA` upload flag
  and cannot be decided from the XML alone (see `docs/VALIDATION_RULES.md`).
- BR-RO-A999 (previously "max 999 invoice lines") is no longer emitted: it does not exist in the
  official CIUS-RO 1.0.9 schematron (removed upstream in schematron version 1.0.8) and was a fabricated
  rule in this library. Documents with more than 999 lines no longer fail local validation on this code.
- BR-CO-17's local tolerance was widened to match the official schematron's own tolerance window
  (previously a flat ≤0.01, which was stricter than what ANAF actually accepts and could reject valid
  documents locally); see "Tolerances" in `docs/VALIDATION_RULES.md`.

### Changed

- `DownloadMessageAsync`/`ProcessDownloadedInvoiceAsync`/`ProcessMultipleInvoicesAsync` now process the
  ANAF ZIP entirely in memory (no temporary files or directories).
- `IAnafEInvoiceClient` is now registered as a typed `HttpClient` (`AddHttpClient<IAnafEInvoiceClient, AnafEInvoiceClient>()`).
- Every `IAnafEInvoiceClient` member that calls ANAF now accepts a `CancellationToken`, as does the new
  `ExchangeAuthorizationCodeAsync(code, options, cancellationToken)` overload and
  `RefreshAccessTokenAsync` on `IAnafOAuthClient`. The certificate-based `GetAccessTokenAsync` overloads,
  `GenerateAuthorizationUrl`, the 4-string `ExchangeAuthorizationCodeAsync`, local
  `ValidateInvoiceXmlAsync`, and the obsolete `DownloadEInvoiceAsync` are unchanged and take no `CancellationToken`.

## [1.1.2]

### Changed
- Documentation improvements and expanded API reference.

## [1.1.1]

### Added
- MIT License file and package license metadata

## [1.1.0]

### Added
- .NET 10 support via multi-targeting (net9.0 and net10.0)

### Changed
- Microsoft.Extensions packages now use version-specific references per target framework

## [1.0.4]

### Added
- Certificate-based authentication with Romanian CA auto-discovery.
- OAuth 2.0 web redirect flow support.
- ANAF eFactura API client for listing, downloading, validating, and uploading invoices.
- Local UBL 2.1 processing and RO_CIUS validation.
- Extension methods for invoice analysis and party data extraction.
- Processing statistics tracking.

### Notes
- This is the first documented release. No migration steps required.
