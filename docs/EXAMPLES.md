# RoEFactura Examples

This document contains longer examples for common application types. These are illustrative and can be adapted to your needs.

## WPF Desktop App (Certificate-based)

```csharp
using Microsoft.Extensions.DependencyInjection;
using RoEFactura;
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
            services.AddRoEFactura();
            services.AddLogging();

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
                TokenTextBox.Text, 30, CuiTextBox.Text);

            InvoicesTextBox.Text = string.Join(Environment.NewLine, invoices.Select(i => i.Id));
        }
    }
}
```

## ASP.NET Core Web API (OAuth)

### Program.cs

```csharp
using RoEFactura;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSession();

builder.Services.AddRoEFacturaWithOAuth(builder.Configuration, "AnafOAuth");

var app = builder.Build();
app.UseSession();
app.MapControllers();
app.Run();
```

### Controller

```csharp
using Microsoft.AspNetCore.Mvc;
using RoEFactura.Models;
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;

[ApiController]
[Route("api/efactura")]
public class EFacturaController : ControllerBase
{
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
    public async Task<IActionResult> Callback(string code, string state)
    {
        var savedState = HttpContext.Session.GetString("oauth_state");
        if (savedState != state)
        {
            return BadRequest("Invalid state");
        }

        var token = await _authClient.ExchangeAuthorizationCodeAsync(code, _options);
        HttpContext.Session.SetString("access_token", token.AccessToken);
        return Redirect("/dashboard?authorized=true");
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string cui, [FromQuery] int days = 30)
    {
        var accessToken = HttpContext.Session.GetString("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            return Unauthorized("Missing access token");
        }

        var invoices = await _invoiceClient.ListEInvoicesAsync(accessToken, days, cui);
        return Ok(invoices);
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
using RoEFactura.Services.Api;
using RoEFactura.Services.Authentication;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services => services.AddRoEFactura())
    .Build();

var auth = host.Services.GetRequiredService<IAnafOAuthClient>();
var invoices = host.Services.GetRequiredService<IAnafEInvoiceClient>();

var token = await auth.GetAccessTokenAsync(clientId, clientSecret, "https://localhost/callback");
var list = await invoices.ListEInvoicesAsync(token.AccessToken, 30, "RO12345678");

foreach (var item in list)
{
    Console.WriteLine(item.Id);
}
```
