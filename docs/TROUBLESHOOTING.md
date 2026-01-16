# Troubleshooting

This guide covers common issues when integrating with ANAF eFactura using RoEFactura.

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

### OAuth callback not reached

Symptoms:
- Browser fails to reach the callback URL after login

Actions:
1. Ensure the callback endpoint exists and is reachable.
2. Use `http://localhost:{port}` for local development and register the exact URL with ANAF.
3. Check firewall, reverse proxies, and HTTPS settings.

## API Requests

### 401 Unauthorized or 403 Forbidden

Actions:
1. Check token expiry and refresh or re-authenticate.
2. Ensure the token is used as `Authorization: Bearer {token}`.
3. Verify the CUI is correct and the OAuth client has proper permissions.

### 429 Too Many Requests

Actions:
1. Add retry with exponential backoff.
2. Cache tokens to avoid excessive auth calls.
3. Reduce concurrency where possible.

### 5xx Errors

Actions:
1. Retry with backoff.
2. Check ANAF service status and try again later.

## Validation Errors (RO_CIUS)

Symptoms:
- `ProcessingResult.IsSuccess == false`
- `ProcessingResult.Errors` contains `ValidationFailure` entries

Actions:
1. Inspect `ErrorCode` and `ErrorMessage` for the exact rule.
2. Check `docs/VALIDATION_RULES.md` for rule details.
3. Ensure your UBL data includes required fields (invoice number, dates, totals).

## TokenExchangeException Guidance

`TokenExchangeException.ErrorType` values:
- `NetworkError`: DNS or connection issues.
- `Timeout`: request timeout.
- `AuthenticationFailed`: invalid credentials or rejected by ANAF.
- `InvalidRequest`: malformed request or invalid redirect URL.
- `InvalidResponse`: unexpected response format.
- `RateLimited`: throttling by ANAF.
- `ServiceUnavailable`: ANAF service temporarily unavailable.
- `ServerError`: ANAF internal error.

## Logging Tips

RoEFactura uses `Microsoft.Extensions.Logging`. Enable debug-level logs to get detailed context:

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Debug);
```
