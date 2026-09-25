# Troubleshooting

This guide covers common issues when integrating with ANAF eFactura using RoEFactura.

## Environment: TEST vs PRODUCTION

RoEFactura defaults to the ANAF **TEST** environment (`https://api.anaf.ro/test/FCTEL/rest`).
Production requires an explicit opt-in.

### "My upload succeeds but I never see it in the real SPV"

Symptoms:
- Uploads, status checks and lists all appear to work, but nothing shows up in the production SPV
  portal for the company.

Actions:
1. Check your configuration: without `"RoEFactura": { "Environment": "Production" }` (or
   `services.AddRoEFactura(o => o.Environment = AnafEnvironment.Production)`), every call goes to
   ANAF TEST, which is a separate sandbox with its own test messages and does not mirror production.
2. Confirm you passed `configuration` into `AddRoEFactura`/`AddRoEFacturaWithOAuth` — if you call
   `AddRoEFactura()` with no `IConfiguration` argument, the `RoEFactura` section is never bound and the
   client always resolves TEST.
3. Remember that `ValidateWithAnafAsync`/`ConvertToPdfAsync` always target production regardless of
   `Environment` — a successful validate/PDF call does not mean the environment is production.

### "`ApiBaseUrl` is ignored" / `InvalidOperationException: RoEFactura:ApiBaseUrl must be an absolute http(s) URL`

Actions:
1. `ApiBaseUrl` must be a fully-qualified `http://` or `https://` URL, not a relative path or a bare
   host name.
2. This exception is thrown when `IAnafEInvoiceClient` is **constructed** (the constructor resolves
   both base URLs eagerly), not lazily on the first API call. With the DI registration, that means it
   surfaces the first time `IAnafEInvoiceClient` is resolved from the container (typically the first
   request/scope that injects it) — validate your configuration early if you want an earlier, explicit
   fail-fast check.

## Certificate Authentication

### "No valid Romanian certificates found"

Symptoms:
- `InvalidOperationException` during `GetAccessTokenAsync`
- Message references missing certificate or no valid certificates

Actions:
1. Install a Romanian digital certificate from a supported CA (CERTSIGN, DIGISIGN, ALFASIGN, CERTDIGITAL).
2. Ensure the certificate is in `CurrentUser/Personal` and has a private key.
3. Confirm the certificate is valid for client authentication.

### "Multiple valid certificates found"

Symptoms:
- `InvalidOperationException` stating multiple certificates are found

Actions:
1. Remove extra Romanian certificates from the store.
2. Or use the thumbprint overload:

```csharp
Token token = await anafOAuthClient.GetAccessTokenAsync(
    thumbprint, clientId, clientSecret, callbackUrl);
```

## OAuth Redirect Flow

### "Token exchange failed" (invalid_client, invalid_request)

Symptoms:
- `TokenExchangeException` with `AuthenticationFailed` or `InvalidRequest`
- Response includes `invalid_client` or `invalid_request`

Actions:
1. Verify `ClientId` and `ClientSecret` from ANAF portal.
2. Ensure `RedirectUri` matches exactly the registered callback URL.
3. Confirm the authorization code is used once and within its valid time window.

### `TokenExchangeErrorType.InvalidGrant` — re-authorize

Symptoms:
- `TokenExchangeException` with `ErrorType == TokenExchangeErrorType.InvalidGrant`, from either
  `ExchangeAuthorizationCodeAsync` or `RefreshAccessTokenAsync`.
- ANAF's underlying error was `invalid_grant` on a 400 response.

Meaning:
- The authorization code was already used, expired, or does not match the redirect URI; or the refresh
  token has been revoked, expired (see `Token.RefreshTokenExpiresAtUtc`, ~365 days by current ANAF
  policy), or was already exchanged.

Actions:
1. Do not retry the same code/refresh token — it will keep failing.
2. Discard the stored token and send the user through `GenerateAuthorizationUrl` again to obtain a
   fresh authorization code.
3. If this happens right after a successful `RefreshAccessTokenAsync`, check you are not calling
   refresh twice concurrently with the same refresh token: ANAF typically issues a new refresh token on
   a successful refresh, so reusing an already-superseded one can fail with `InvalidGrant`. Always store
   and use the newest `Token.RefreshToken`.

### OAuth callback not reached

Symptoms:
- Browser fails to reach the callback URL after login

Actions:
1. Ensure the callback endpoint exists and is reachable.
2. Use `http://localhost:{port}` for local development and register the exact URL with ANAF.
3. Check firewall, reverse proxies, and HTTPS settings.

## API Requests

### 401 Unauthorized or 403 Forbidden

Symptoms:
- `AnafApiException` with `IsUnauthorized == true`, from any `IAnafEInvoiceClient` member (list calls
  included). Existing `catch (HttpRequestException)` handlers still catch it, since `AnafApiException`
  is a subclass; catch `AnafApiException` first if you need `IsUnauthorized`/`RawResponse`/`Errors`.

Actions:
1. Check `AnafApiException.IsUnauthorized` (true for both 401 and 403) before deciding how to react.
2. Check token expiry (`Token.ExpiresAtUtc`) and refresh via `RefreshAccessTokenAsync`, or re-authenticate.
3. Ensure the token is used as `Authorization: Bearer {token}` (handled automatically by this library).
4. Verify the CUI is correct and the OAuth client has proper permissions/rights in SPV for that CUI.
5. Confirm you are calling the environment (TEST/Production) that actually granted the token — a TEST
   token will be rejected by the production base URL and vice versa.

### 429 Too Many Requests

Symptoms:
- `AnafRateLimitException` (subclass of `AnafApiException`/`HttpRequestException`).

Actions:
1. Read `AnafRateLimitException.RetryAfter` (parsed from ANAF's `Retry-After` header, when present) and
   wait at least that long before retrying. This library does not retry automatically.
2. Check the official limits table in the [README](../README.md#official-anaf-limits) — the global
   limit is 1,000 calls/minute; `/stare`, `/lista` and `/descarcare` have additional per-message or
   per-CUI daily limits.
3. Cache tokens and list results to avoid excessive calls; reduce concurrency where possible.
4. Repeatedly ignoring 429s can lead ANAF to block API access for the user or application — do not
   loop-retry without backoff.

### `AnafApiException` with no obvious cause

Actions:
1. Inspect `AnafApiException.RawResponse` and `Errors` for ANAF's own error text.
2. Confirm the request body was raw XML with `Content-Type: text/plain` — `UploadAsync`,
   `ValidateWithAnafAsync` and `ConvertToPdfAsync` all use this, not multipart form data.
3. Check the XML size limits: 10 MB for `UploadAsync`, 5 MB for `ValidateWithAnafAsync`/`ConvertToPdfAsync`.

### "XML cu erori nepreluat de sistem" (`AnafMessageState.RejectedAtUpload`)

Meaning:
- The file was rejected before entering ANAF's processing queue — the error was received directly as
  the response to the `upload`/`uploadb2c` call itself, not via `stareMesaj`.

Actions:
1. This state comes from `GetMessageStatusAsync`'s `AnafMessageStatusResult.State`, but the real error
   detail is on the original `UploadAsync` response's `AnafUploadResult.Errors` (or the upload's
   `AnafApiException`/`AnafRateLimitException` if the HTTP call itself failed) — check that first.
2. Common causes: malformed XML, wrong `standard` query value for the document's actual UBL/CII shape,
   or a `cif` the caller has no SPV rights for.
3. Run `ValidateWithAnafAsync` (or local `ValidateInvoiceXmlAsync`) before uploading to catch structural
   problems earlier.

### The 60-day download window has expired

Symptoms:
- `AnafDownloadWindowExpiredException` from `DownloadMessageAsync`, `ProcessDownloadedInvoiceAsync`, or
  the obsolete `DownloadEInvoiceAsync`.

Meaning:
- ANAF only keeps a `descarcare` response available for a limited window (typically 60 days) after it
  was generated; after that, the download id is no longer retrievable.

Actions:
1. `AnafDownloadWindowExpiredException.AnafDownloadId`/`AnafErrorMessage` identify which id expired.
2. Download and archive invoices promptly after `stareMesaj` reports `Ok`/`Nok` — do not rely on being
   able to re-download weeks later.
3. There is no workaround via the API once the window has passed; the invoice/error content must be
   sourced from your own archive or re-requested from the counterparty.

### 5xx Errors

Actions:
1. Retry with backoff (this library does not retry automatically).
2. Check ANAF service status and try again later.

## Validation Errors (CIUS-RO / RO_CIUS)

Symptoms:
- `ProcessingResult.IsSuccess == false`
- `ProcessingResult.Errors` contains `ValidationFailure` entries

Actions:
1. Inspect `ErrorCode` and `ErrorMessage` for the exact rule.
2. Check [`docs/VALIDATION_RULES.md`](VALIDATION_RULES.md) for rule details, sources and known
   tolerances/leniencies.
3. Ensure your UBL data includes required fields (invoice number, dates, totals).
4. Remember local validation is a fast pre-check, not the final authority — a document that fails
   `ValidateWithAnafAsync` is definitively rejected; a document that passes every local rule may still
   fail an ANAF rule not implemented locally (see "Out of scope" in `VALIDATION_RULES.md`).

### BR-RO-110 / BR-RO-100 (county and Bucharest sector) failures

Symptoms:
- `BR-RO-110` (seller) / `BR-RO-111` (buyer): county code not recognized.
- `BR-RO-100` (seller) / `BR-RO-101` (buyer): Bucharest address city not recognized.

Actions:
1. **County codes must carry the `RO-` prefix**: use `RO-CJ`, not `CJ` or `Cluj`. See
   `RomanianConstants.ValidCountyCodes` for the full 42-code list (exact match, case-sensitive).
2. **Bucharest addresses (`county = RO-B`) must use an exact `SECTORn` city**: `SECTOR1`..`SECTOR6`.
   `Sector 3`, `sector3`, `Bucuresti`, and `SECTOR 1` (with a space) all fail — the match is exact and
   case-sensitive, matching the official schematron.
3. These rules only apply when the party's country is `RO`; foreign parties without a county are not
   evaluated by BR-RO-100/101/110/111 (see `ForeignBuyerWithoutCounty_PassesAddressRules` in the test suite).

## TokenExchangeException Guidance

`TokenExchangeException.ErrorType` values:
- `NetworkError`: DNS or connection issues.
- `Timeout`: request timeout.
- `AuthenticationFailed`: invalid credentials or rejected by ANAF (401).
- `InvalidRequest`: malformed request or invalid redirect URL (400, not `invalid_grant`).
- `InvalidGrant`: authorization code or refresh token invalid, expired or revoked — see above; do not retry, re-authorize.
- `InvalidResponse`: unexpected response format.
- `RateLimited`: throttling by ANAF (429).
- `ServiceUnavailable`: ANAF service temporarily unavailable (503).
- `ServerError`: ANAF internal error (500).
- `UnknownError`: any other non-2xx status not covered above.

## Logging Tips

RoEFactura uses `Microsoft.Extensions.Logging` exclusively (no `Console.*` output). Enable debug-level
logs to get detailed context:

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```

**No PII or sensitive content ever appears in RoEFactura's own logs**, by design:
- XML content, request bodies, party names and addresses are never logged.
- Access/refresh token values and full token response bodies are never logged; tokens are represented
  only by a short fingerprint in log messages.
- CIF/CUI values only appear as part of a logged request URL (already public in that context), never
  as a separately-labeled sensitive field.
- Non-success response bodies are logged only as a ≤300-character preview, and only when the response's
  media type is JSON or XML.

If you add your own logging around this library's calls (e.g. logging the invoice XML you're about to
upload, or the raw token response), apply the same discipline yourself — RoEFactura's guarantee covers
only what the library logs internally.
