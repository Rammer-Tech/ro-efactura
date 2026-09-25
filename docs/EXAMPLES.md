# RoEFactura Examples

This document contains longer examples for common application types. These are illustrative and can be
adapted to your needs. All examples target RoEFactura 2.0.0: ANAF **TEST** by default, `net10.0`.

## WPF Desktop App (Certificate-based)

```csharp
using Microsoft.Extensions.DependencyInjection;
using RoEFactura;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EFacturaDesktopApp
{
    public partial class MainWindow : Window
    {
        private readonly IAnafOAuthClient _authClient;
        private readonly IAnafEInvoiceClient _invoiceClient;

        public MainWindow()
        {
            InitializeComponent();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddRoEFactura(); // ANAF TEST by default; opt in to Production only when you deploy
            // services.AddRoEFactura(options => options.Environment = AnafEnvironment.Production);

            var provider = services.BuildServiceProvider();
            _authClient = provider.GetRequiredService<IAnafOAuthClient>();
            _invoiceClient = provider.GetRequiredService<IAnafEInvoiceClient>();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var token = await _authClient.GetAccessTokenAsync(
                    ClientIdTextBox.Text,
                    ClientSecretTextBox.Text,
                    "https://yourapp.com/callback");

                TokenTextBox.Text = token.AccessToken;
                InvoicePanel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Authentication Failed");
            }
        }

        private async void GetInvoicesButton_Click(object sender, RoutedEventArgs e)
        {
            var invoices = await _invoiceClient.ListEInvoicesAsync(
                TokenTextBox.Text, 30, CuiTextBox.Text, filter: null, CancellationToken.None);

            InvoicesTextBox.Text = string.Join(Environment.NewLine, invoices.Select(i => i.Id));
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                byte[] xmlBytes = System.IO.File.ReadAllBytes(InvoiceFilePathTextBox.Text);

                var result = await _invoiceClient.UploadAsync(
                    TokenTextBox.Text,
                    xmlBytes,
                    new AnafUploadOptions { Cif = CuiTextBox.Text },
                    CancellationToken.None);

                if (result.IsSuccess)
                {
                    UploadIndexTextBox.Text = result.UploadIndex;
                }
                else
                {
                    MessageBox.Show(string.Join(Environment.NewLine, result.Errors), "Upload rejected");
                }
            }
            catch (AnafRateLimitException ex)
            {
                MessageBox.Show($"Rate limited, retry after {ex.RetryAfter}", "Too many requests");
            }
            catch (AnafApiException ex)
            {
                MessageBox.Show(ex.Message, "Upload failed");
            }
        }
    }
}
```

## ASP.NET Core Web API (OAuth, with refresh)

### Program.cs

```csharp
using RoEFactura;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSession();

// Binds both "AnafOAuth" and "RoEFactura" sections from appsettings.json.
builder.Services.AddRoEFacturaWithOAuth(builder.Configuration, "AnafOAuth");

var app = builder.Build();
app.UseSession();
app.MapControllers();
app.Run();
```

### appsettings.json

```json
{
  "AnafOAuth": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret",
    "RedirectUri": "https://yourapp.com/api/efactura/oauth/callback"
  },
  "RoEFactura": {
    "Environment": "Test"
  }
}
```

Change `"Environment": "Test"` to `"Production"` only when you deploy against real SPV data.

### Controller

```csharp
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;

[ApiController]
[Route("api/efactura")]
public class EFacturaController : ControllerBase
{
    // Per-session refresh coordination for a single application instance: prevents two concurrent
    // requests for the same session from racing to refresh with the same (about-to-be-rotated)
    // refresh token, where the losing request would otherwise get InvalidGrant. For a multi-instance
    // deployment, replace this with a distributed lock (e.g. Redis-backed) or equivalent coordination.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RefreshLocks = new();

    private readonly IAnafOAuthClient _authClient;
    private readonly IAnafEInvoiceClient _invoiceClient;
    private readonly AnafOAuthOptions _options;

    public EFacturaController(
        IAnafOAuthClient authClient,
        IAnafEInvoiceClient invoiceClient,
        AnafOAuthOptions options)
    {
        _authClient = authClient;
        _invoiceClient = invoiceClient;
        _options = options;
    }

    [HttpPost("oauth/initiate")]
    public IActionResult InitiateOAuth()
    {
        var state = Guid.NewGuid().ToString("N");
        HttpContext.Session.SetString("oauth_state", state);

        var authUrl = _authClient.GenerateAuthorizationUrl(_options, state);
        return Ok(new { authorizationUrl = authUrl, state });
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback(string code, string state, CancellationToken ct)
    {
        var savedState = HttpContext.Session.GetString("oauth_state");
        if (savedState != state)
        {
            return BadRequest("Invalid state");
        }

        var token = await _authClient.ExchangeAuthorizationCodeAsync(code, _options, ct);
        HttpContext.Session.SetString("access_token", token.AccessToken);
        HttpContext.Session.SetString("refresh_token", token.RefreshToken ?? string.Empty);
        HttpContext.Session.SetString("expires_at", token.ExpiresAtUtc!.Value.ToString("O"));
        return Redirect("/dashboard?authorized=true");
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] string cui, [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var accessToken = await GetValidAccessTokenAsync(ct);
        if (accessToken is null)
        {
            return Unauthorized("Missing or expired access token");
        }

        try
        {
            var invoices = await _invoiceClient.ListEInvoicesAsync(accessToken, days, cui, filter: null, ct);
            return Ok(invoices);
        }
        catch (AnafRateLimitException ex)
        {
            Response.Headers["Retry-After"] = ((int)(ex.RetryAfter?.TotalSeconds ?? 60)).ToString();
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }
        catch (AnafApiException ex) when (ex.IsUnauthorized)
        {
            return Unauthorized(ex.Message);
        }
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] IFormFile invoice, [FromQuery] string cui, CancellationToken ct)
    {
        var accessToken = await GetValidAccessTokenAsync(ct);
        if (accessToken is null)
        {
            return Unauthorized("Missing or expired access token");
        }

        await using var stream = new MemoryStream();
        await invoice.CopyToAsync(stream, ct);

        var result = await _invoiceClient.UploadAsync(
            accessToken, stream.ToArray(), new AnafUploadOptions { Cif = cui }, ct);

        return result.IsSuccess
            ? Ok(new { uploadIndex = result.UploadIndex, responseDate = result.ResponseDate })
            : UnprocessableEntity(new { errors = result.Errors });
    }

    [HttpGet("status/{uploadIndex}")]
    public async Task<IActionResult> Status(string uploadIndex, CancellationToken ct)
    {
        var accessToken = await GetValidAccessTokenAsync(ct);
        if (accessToken is null)
        {
            return Unauthorized("Missing or expired access token");
        }

        var status = await _invoiceClient.GetMessageStatusAsync(accessToken, uploadIndex, ct);
        return Ok(new { state = status.State.ToString(), downloadId = status.DownloadId, errors = status.Errors });
    }

    [HttpGet("download/{downloadId}")]
    public async Task<IActionResult> Download(string downloadId, CancellationToken ct)
    {
        var accessToken = await GetValidAccessTokenAsync(ct);
        if (accessToken is null)
        {
            return Unauthorized("Missing or expired access token");
        }

        try
        {
            var result = await _invoiceClient.DownloadMessageAsync(accessToken, downloadId, ct);
            return result.DocumentXml is not null
                ? File(result.DocumentXml, "application/xml", result.DocumentFileName ?? "invoice.xml")
                : NotFound("No invoice XML found (signature-only response).");
        }
        catch (AnafDownloadWindowExpiredException ex)
        {
            return NotFound(new { message = ex.Message, downloadId = ex.AnafDownloadId });
        }
    }

    private async Task<string?> GetValidAccessTokenAsync(CancellationToken ct)
    {
        var accessToken = HttpContext.Session.GetString("access_token");
        var expiresAtRaw = HttpContext.Session.GetString("expires_at");
        if (string.IsNullOrEmpty(accessToken))
        {
            return null;
        }

        // Only refresh when the access token is actually close to expiry — never on every request.
        var expiresAt = DateTimeOffset.TryParse(expiresAtRaw, out var parsed) ? parsed : DateTimeOffset.MinValue;
        if (expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
        {
            return accessToken;
        }

        // Refresh path: serialize concurrent requests for this session so they don't race to use the
        // same (about-to-be-rotated) refresh token. See RefreshLocks above for the multi-instance caveat.
        var sessionLock = RefreshLocks.GetOrAdd(HttpContext.Session.Id, _ => new SemaphoreSlim(1, 1));
        await sessionLock.WaitAsync(ct);
        try
        {
            // Re-read: another request for this session may have refreshed while we were waiting.
            accessToken = HttpContext.Session.GetString("access_token");
            var refreshToken = HttpContext.Session.GetString("refresh_token");
            expiresAtRaw = HttpContext.Session.GetString("expires_at");
            expiresAt = DateTimeOffset.TryParse(expiresAtRaw, out parsed) ? parsed : DateTimeOffset.MinValue;
            if (expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
            {
                return accessToken;
            }

            if (string.IsNullOrEmpty(refreshToken))
            {
                // Access token is expired (or expiring) and there is no refresh token to use — return
                // null (never the stale access token) so the caller re-authorizes.
                return null;
            }

            try
            {
                var refreshed = await _authClient.RefreshAccessTokenAsync(refreshToken, _options, ct);
                HttpContext.Session.SetString("access_token", refreshed.AccessToken);
                // The response may omit a new refresh token; keep the previous one when that happens.
                HttpContext.Session.SetString(
                    "refresh_token",
                    string.IsNullOrEmpty(refreshed.RefreshToken) ? refreshToken : refreshed.RefreshToken);
                HttpContext.Session.SetString("expires_at", refreshed.ExpiresAtUtc!.Value.ToString("O"));
                return refreshed.AccessToken;
            }
            catch (TokenExchangeException ex) when (ex.ErrorType == TokenExchangeErrorType.InvalidGrant)
            {
                // Refresh token is dead; caller must go through GenerateAuthorizationUrl again.
                return null;
            }
        }
        finally
        {
            sessionLock.Release();
        }
    }
}
```

## React Frontend (Calls API)

### Service

```javascript
import axios from 'axios';

class EFacturaService {
  constructor() {
    this.client = axios.create({ baseURL: '/api/efactura', withCredentials: true });
  }

  async initiateOAuth() {
    const response = await this.client.post('/oauth/initiate');
    return response.data;
  }

  async getInvoices(cui, days = 30) {
    const response = await this.client.get('/invoices', { params: { cui, days } });
    return response.data;
  }

  async uploadInvoice(cui, file) {
    const form = new FormData();
    form.append('invoice', file);
    const response = await this.client.post('/upload', form, { params: { cui } });
    return response.data;
  }

  async getStatus(uploadIndex) {
    const response = await this.client.get(`/status/${uploadIndex}`);
    return response.data;
  }
}

export default new EFacturaService();
```

### Component

```jsx
import React, { useState } from 'react';
import eFacturaService from '../services/eFacturaService';

export default function EFacturaIntegration() {
  const [loading, setLoading] = useState(false);
  const [cui, setCui] = useState('');
  const [invoices, setInvoices] = useState(null);

  const handleAuthorize = async () => {
    setLoading(true);
    const result = await eFacturaService.initiateOAuth();
    window.location.href = result.authorizationUrl;
  };

  const handleGetInvoices = async () => {
    setLoading(true);
    const data = await eFacturaService.getInvoices(cui, 30);
    setInvoices(data);
    setLoading(false);
  };

  return (
    <div>
      <button onClick={handleAuthorize} disabled={loading}>Connect to ANAF</button>
      <input value={cui} onChange={(e) => setCui(e.target.value)} placeholder="CUI" />
      <button onClick={handleGetInvoices} disabled={loading}>Get Invoices</button>
      <pre>{JSON.stringify(invoices, null, 2)}</pre>
    </div>
  );
}
```

## Console App

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoEFactura;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
        services.AddRoEFactura(context.Configuration)) // "RoEFactura" section from appsettings.json; defaults to TEST
    .Build();

var auth = host.Services.GetRequiredService<IAnafOAuthClient>();
var invoices = host.Services.GetRequiredService<IAnafEInvoiceClient>();

var token = await auth.GetAccessTokenAsync(clientId, clientSecret, "https://localhost/callback");
var list = await invoices.ListEInvoicesAsync(token.AccessToken, 30, "RO12345678", filter: null, CancellationToken.None);

foreach (var item in list)
{
    Console.WriteLine(item.Id);
}
```

## Upload → status → download workflow (console)

```csharp
using RoEFactura.Models;

byte[] xmlBytes = File.ReadAllBytes("invoice.xml");

// 1. Optional: validate locally and with ANAF's public service before spending an upload attempt.
var local = await invoices.ValidateInvoiceXmlAsync(File.ReadAllText("invoice.xml"));
if (!local.IsSuccess)
{
    foreach (var error in local.Errors)
        Console.WriteLine($"local: {error.ErrorCode} {error.ErrorMessage}");
    return;
}

var anafValidation = await invoices.ValidateWithAnafAsync(xmlBytes, AnafDocumentStandard.Ubl);
if (!anafValidation.IsValid)
{
    foreach (var message in anafValidation.Messages)
        Console.WriteLine($"anaf: {message}");
    return;
}

// 2. Upload.
var uploadResult = await invoices.UploadAsync(
    token.AccessToken, xmlBytes, new AnafUploadOptions { Cif = "12345678" });

if (!uploadResult.IsSuccess)
{
    throw new InvalidOperationException(string.Join("; ", uploadResult.Errors));
}

// 3. Poll status until it's no longer "in prelucrare".
AnafMessageStatusResult status;
do
{
    await Task.Delay(TimeSpan.FromSeconds(5));
    status = await invoices.GetMessageStatusAsync(token.AccessToken, uploadResult.UploadIndex!);
}
while (status.State == AnafMessageState.InProcessing);

if (status.State != AnafMessageState.Ok || status.DownloadId is null)
{
    throw new InvalidOperationException($"Upload rejected: {string.Join("; ", status.Errors)}");
}

// 4. Download and process the accepted invoice, entirely in memory.
var processed = await invoices.ProcessDownloadedInvoiceAsync(token.AccessToken, status.DownloadId);
if (processed.IsSuccess)
{
    Console.WriteLine($"Accepted: {processed.Data!.ID.Value}, total {processed.Data.GetTotalAmountDue()}");
}
```
