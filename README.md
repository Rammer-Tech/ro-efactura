# RoEFactura - Romanian ANAF eInvoicing Integration

[![NuGet Version](https://img.shields.io/nuget/v/RoEFactura)](https://www.nuget.org/packages/RoEFactura)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)

**RoEFactura** is a comprehensive .NET library for seamless integration with the Romanian ANAF (National Agency for Fiscal Administration) eInvoicing system. It provides both certificate-based authentication for desktop applications and OAuth 2.0 flow for web applications.

## 🚀 Features

- **Dual Authentication Support**
  - 🖥️ **Certificate-based** authentication (Desktop/Server applications)
  - 🌐 **OAuth 2.0 redirect flow** (Web applications)
- **Complete ANAF API Coverage**
  - List electronic invoices (paged and non-paged)
  - Download invoices as ZIP files
  - Validate XML invoices
  - Upload XML invoices
- **Romanian Certificate Auto-Discovery**
  - Supports CERTSIGN, DIGISIGN, ALFASIGN, CERTDIGITAL
  - Automatic certificate validation
- **Production Ready**
  - Comprehensive error handling
  - Logging integration
  - Configurable environments

---

## 📦 Installation

### NuGet Package Manager
```bash
Install-Package RoEFactura
```

### .NET CLI
```bash
dotnet add package RoEFactura
```

### PackageReference
```xml
<PackageReference Include="RoEFactura" Version="1.0.0" />
```

---

## ⚡ Quick Start

### For Desktop Applications (Certificate-based)

```csharp
using RoEFactura;
using RoEFactura.Services.Authentication;

// 1. Register services
services.AddRoEFactura();

// 2. Use the client
public class InvoiceService
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly IAnafEInvoiceClient _invoiceClient;

    public InvoiceService(IAnafOAuthClient anafClient, IAnafEInvoiceClient invoiceClient)
    {
        _anafClient = anafClient;
        _invoiceClient = invoiceClient;
    }

    public async Task<string> GetInvoicesAsync()
    {
        // Get access token using certificate
        var token = await _anafClient.GetAccessTokenAsync(
            "your_client_id", 
            "your_client_secret", 
            "your_callback_url"
        );

        // Use token to fetch invoices
        var invoices = await _invoiceClient.ListEInvoicesAsync(
            token.AccessToken, 
            days: 30, 
            cui: "your_cui"
        );

        return invoices;
    }
}
```

### For Web Applications (OAuth Flow)

```csharp
using RoEFactura;
using RoEFactura.Models;

// 1. Register services with OAuth configuration
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");

// 2. Configure in appsettings.json
{
  "AnafOAuth": {
    "ClientId": "your_anaf_client_id",
    "ClientSecret": "your_anaf_client_secret",
    "RedirectUri": "https://yourapp.com/api/oauth/callback"
  }
}

// 3. OAuth Controller
[ApiController]
[Route("api/[controller]")]
public class OAuthController : ControllerBase
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly AnafOAuthOptions _options;

    public OAuthController(IAnafOAuthClient anafClient, AnafOAuthOptions options)
    {
        _anafClient = anafClient;
        _options = options;
    }

    [HttpPost("initiate")]
    public IActionResult InitiateOAuth()
    {
        // Generate authorization URL
        var state = GenerateSecureState(); // Your CSRF state
        var authUrl = _anafClient.GenerateAuthorizationUrl(_options, state);
        
        return Ok(new { authorizationUrl = authUrl, state });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string code, string state)
    {
        // Validate state (CSRF protection)
        if (!ValidateState(state)) return BadRequest("Invalid state");

        // Exchange code for token
        var token = await _anafClient.ExchangeAuthorizationCodeAsync(code, _options);
        
        // Store token securely and redirect
        await StoreTokenSecurely(token);
        return Redirect("/dashboard?success=true");
    }
}
```

---

## 🔐 Authentication Methods

### Method 1: Certificate-Based Authentication

Perfect for **desktop applications**, **console apps**, and **server-to-server** integrations.

#### Auto-Discovery (Recommended)
```csharp
public async Task AuthenticateWithAutoDiscovery()
{
    try 
    {
        var token = await _anafClient.GetAccessTokenAsync(
            clientId: "your_client_id",
            clientSecret: "your_client_secret", 
            callbackUrl: "your_callback_url"
        );
        
        Console.WriteLine($"Access Token: {token.AccessToken}");
        Console.WriteLine($"Expires: {DateTime.UtcNow.AddSeconds(token.ExpiresIn)}");
    }
    catch (InvalidOperationException ex)
    {
        // Handle certificate not found or multiple certificates
        Console.WriteLine($"Certificate error: {ex.Message}");
    }
}
```

#### Specific Certificate by Thumbprint
```csharp
public async Task AuthenticateWithSpecificCertificate()
{
    // Get available certificates
    var certificates = AnafOAuthClient.GetAvailableRomanianCertificates();
    
    foreach (var cert in certificates)
    {
        Console.WriteLine($"Certificate: {cert.Subject}");
        Console.WriteLine($"Thumbprint: {cert.Thumbprint}");
        Console.WriteLine($"Valid for client auth: {cert.IsValidForClientAuth}");
        Console.WriteLine($"Expires: {cert.ExpiryDate}");
        Console.WriteLine("---");
    }

    // Use specific certificate
    var selectedThumbprint = certificates.First().Thumbprint;
    var token = await _anafClient.GetAccessTokenAsync(
        thumbprint: selectedThumbprint,
        clientId: "your_client_id",
        clientSecret: "your_client_secret",
        callbackUrl: "your_callback_url"
    );
}
```

#### Using X509Certificate2 Object
```csharp
public async Task AuthenticateWithCertificateObject()
{
    // Load certificate from file or store
    var certificate = new X509Certificate2("path/to/certificate.pfx", "password");
    
    var token = await _anafClient.GetAccessTokenAsync(
        certificate: certificate,
        clientId: "your_client_id",
        clientSecret: "your_client_secret",
        callbackUrl: "your_callback_url"
    );
}
```

### Method 2: OAuth 2.0 Redirect Flow

Perfect for **web applications** where users need to authenticate via browser.

#### Step 1: Generate Authorization URL
```csharp
public class OAuthService
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly AnafOAuthOptions _options;

    public string InitiateOAuth(string state)
    {
        // Generate authorization URL
        var authUrl = _anafClient.GenerateAuthorizationUrl(_options, state);
        
        // URL will look like:
        // https://logincert.anaf.ro/anaf-oauth2/v1/authorize?
        // response_type=code&
        // client_id=your_client_id&
        // redirect_uri=https://yourapp.com/callback&
        // state=your_csrf_state&
        // token_content_type=jwt
        
        return authUrl;
    }
}
```

#### Step 2: Handle OAuth Callback
```csharp
[HttpGet("oauth/callback")]
public async Task<IActionResult> HandleCallback(string code, string state)
{
    try
    {
        // 1. Validate state (CSRF protection)
        if (!IsValidState(state))
        {
            return BadRequest("Invalid state parameter");
        }

        // 2. Exchange authorization code for token
        var token = await _anafClient.ExchangeAuthorizationCodeAsync(code, _options);

        // 3. Store token securely (your implementation)
        await _tokenStore.SaveTokenAsync(GetCurrentUserId(), new StoredToken
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
            TokenType = token.TokenType,
            Scope = token.Scope
        });

        // 4. Redirect to success page
        return Redirect("/dashboard?authorized=true");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "OAuth callback failed");
        return Redirect("/error?message=oauth_failed");
    }
}
```

---

## 🛠️ Configuration

### Method 1: appsettings.json (Recommended)

```json
{
  "AnafOAuth": {
    "ClientId": "your_anaf_oauth_client_id",
    "ClientSecret": "your_anaf_oauth_client_secret", 
    "RedirectUri": "https://yourapp.com/api/oauth/callback",
    "AuthorizeUrl": "https://logincert.anaf.ro/anaf-oauth2/v1/authorize",
    "TokenUrl": "https://logincert.anaf.ro/anaf-oauth2/v1/token",
    "IncludeTokenContentType": true
  }
}
```

```csharp
// Program.cs or Startup.cs
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");
```

### Method 2: Direct Configuration

```csharp
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = "your_client_id",
    ClientSecret = "your_client_secret",
    RedirectUri = "https://yourapp.com/oauth/callback",
    AuthorizeUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize", // optional
    TokenUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/token", // optional
    IncludeTokenContentType = true // optional, default: true
});
```

### Method 3: Environment Variables

```bash
# Set environment variables
export ANAF_CLIENT_ID="your_client_id"
export ANAF_CLIENT_SECRET="your_client_secret"
export ANAF_REDIRECT_URI="https://yourapp.com/oauth/callback"
```

```csharp
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = Environment.GetEnvironmentVariable("ANAF_CLIENT_ID"),
    ClientSecret = Environment.GetEnvironmentVariable("ANAF_CLIENT_SECRET"),
    RedirectUri = Environment.GetEnvironmentVariable("ANAF_REDIRECT_URI")
});
```

---

## 📋 Complete Examples

### 🖥️ Desktop Application Example (WPF)

**MainWindow.xaml.cs**
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoEFactura;
using RoEFactura.Services.Authentication;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace EFacturaDesktopApp
{
    public partial class MainWindow : Window
    {
        private readonly IAnafOAuthClient _anafClient;
        private readonly IAnafEInvoiceClient _invoiceClient;

        public MainWindow()
        {
            InitializeComponent();
            
            // Setup DI container
            var services = new ServiceCollection();
            services.AddRoEFactura();
            services.AddLogging();
            
            var provider = services.BuildServiceProvider();
            _anafClient = provider.GetRequiredService<IAnafOAuthClient>();
            _invoiceClient = provider.GetRequiredService<IAnafEInvoiceClient>();
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadingIndicator.Visibility = Visibility.Visible;
                StatusText.Text = "Authenticating with ANAF...";

                // Get token using certificate
                var token = await _anafClient.GetAccessTokenAsync(
                    ClientIdTextBox.Text,
                    ClientSecretTextBox.Text,
                    "https://yourapp.com/callback"
                );

                StatusText.Text = "Authentication successful!";
                TokenTextBox.Text = token.AccessToken;
                
                // Enable invoice operations
                InvoicePanel.IsEnabled = true;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                MessageBox.Show(ex.Message, "Authentication Failed", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                LoadingIndicator.Visibility = Visibility.Hidden;
            }
        }

        private async void GetInvoicesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var accessToken = TokenTextBox.Text;
                var cui = CuiTextBox.Text;
                var days = int.Parse(DaysTextBox.Text);

                var invoices = await _invoiceClient.ListEInvoicesAsync(accessToken, days, cui);
                
                InvoicesTextBox.Text = invoices;
                StatusText.Text = "Invoices retrieved successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
        }
    }
}
```

### 🌐 ASP.NET Core Web Application Example

**Program.cs**
```csharp
using RoEFactura;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Add RoEFactura with OAuth
builder.Services.AddRoEFacturaWithOAuth(builder.Configuration, "AnafOAuth");

var app = builder.Build();

// Configure pipeline
app.UseSession();
app.UseRouting();
app.MapControllers();

app.Run();
```

**Controllers/EFacturaController.cs**
```csharp
using Microsoft.AspNetCore.Mvc;
using RoEFactura.Models;
using RoEFactura.Services.Authentication;
using System.Security.Cryptography;

[ApiController]
[Route("api/[controller]")]
public class EFacturaController : ControllerBase
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly AnafOAuthOptions _options;
    private readonly ILogger<EFacturaController> _logger;

    public EFacturaController(
        IAnafOAuthClient anafClient, 
        AnafOAuthOptions options,
        ILogger<EFacturaController> logger)
    {
        _anafClient = anafClient;
        _options = options;
        _logger = logger;
    }

    [HttpPost("oauth/initiate")]
    public IActionResult InitiateOAuth()
    {
        try
        {
            // Generate CSRF state
            var state = GenerateSecureState();
            HttpContext.Session.SetString("oauth_state", state);

            // Generate authorization URL
            var authUrl = _anafClient.GenerateAuthorizationUrl(_options, state);

            return Ok(new { 
                success = true, 
                authorizationUrl = authUrl, 
                state = state 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate OAuth");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> OAuthCallback(string code, string state)
    {
        try
        {
            // Validate state
            var savedState = HttpContext.Session.GetString("oauth_state");
            if (string.IsNullOrEmpty(savedState) || savedState != state)
            {
                return BadRequest("Invalid state parameter");
            }

            // Exchange code for token
            var token = await _anafClient.ExchangeAuthorizationCodeAsync(code, _options);

            // Store token in session (use secure storage in production)
            HttpContext.Session.SetString("access_token", token.AccessToken);
            HttpContext.Session.SetString("token_expires", 
                DateTime.UtcNow.AddSeconds(token.ExpiresIn).ToString());

            // Clean up state
            HttpContext.Session.Remove("oauth_state");

            return Redirect("/dashboard?success=true");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OAuth callback failed");
            return Redirect("/error?message=oauth_failed");
        }
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] string cui, [FromQuery] int days = 30)
    {
        try
        {
            var accessToken = HttpContext.Session.GetString("access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                return Unauthorized(new { error = "No access token found" });
            }

            var invoices = await _invoiceClient.ListEInvoicesAsync(accessToken, days, cui);
            return Ok(new { success = true, data = invoices });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve invoices");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    private string GenerateSecureState()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
```

### ⚛️ React Frontend Integration

**services/eFacturaService.js**
```javascript
import axios from 'axios';

class EFacturaService {
  constructor() {
    this.baseURL = '/api/efactura';
    this.client = axios.create({
      baseURL: this.baseURL,
      withCredentials: true
    });
  }

  async initiateOAuth() {
    try {
      const response = await this.client.post('/oauth/initiate');
      return response.data;
    } catch (error) {
      throw new Error(`OAuth initiation failed: ${error.message}`);
    }
  }

  async getInvoices(cui, days = 30) {
    try {
      const response = await this.client.get('/invoices', {
        params: { cui, days }
      });
      return response.data;
    } catch (error) {
      throw new Error(`Failed to get invoices: ${error.message}`);
    }
  }

  async checkAuthStatus() {
    try {
      const response = await this.client.get('/auth/status');
      return response.data;
    } catch (error) {
      return { isAuthenticated: false };
    }
  }
}

export default new EFacturaService();
```

**components/EFacturaIntegration.jsx**
```jsx
import React, { useState, useEffect } from 'react';
import eFacturaService from '../services/eFacturaService';

const EFacturaIntegration = () => {
  const [isAuthorized, setIsAuthorized] = useState(false);
  const [loading, setLoading] = useState(false);
  const [invoices, setInvoices] = useState(null);
  const [cui, setCui] = useState('');

  useEffect(() => {
    checkAuthStatus();
    
    // Handle OAuth callback
    const urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get('success') === 'true') {
      setIsAuthorized(true);
      showSuccessMessage('Successfully connected to ANAF eFactura!');
      // Clean URL
      window.history.replaceState({}, document.title, window.location.pathname);
    }
  }, []);

  const checkAuthStatus = async () => {
    try {
      const status = await eFacturaService.checkAuthStatus();
      setIsAuthorized(status.isAuthenticated);
    } catch (error) {
      console.error('Auth status check failed:', error);
    }
  };

  const handleAuthorize = async () => {
    setLoading(true);
    try {
      const result = await eFacturaService.initiateOAuth();
      if (result.success) {
        // Redirect to ANAF OAuth page
        window.location.href = result.authorizationUrl;
      } else {
        throw new Error(result.error || 'Failed to initiate OAuth');
      }
    } catch (error) {
      alert(`Authorization failed: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleGetInvoices = async () => {
    if (!cui.trim()) {
      alert('Please enter a CUI');
      return;
    }

    setLoading(true);
    try {
      const result = await eFacturaService.getInvoices(cui, 30);
      setInvoices(result.data);
    } catch (error) {
      alert(`Failed to get invoices: ${error.message}`);
    } finally {
      setLoading(false);
    }
  };

  const showSuccessMessage = (message) => {
    // Implementation for success notification
    const notification = document.createElement('div');
    notification.className = 'alert alert-success';
    notification.textContent = message;
    document.body.appendChild(notification);
    setTimeout(() => notification.remove(), 3000);
  };

  return (
    <div className="container">
      <h2>🇷🇴 ANAF eFactura Integration</h2>
      
      <div className="card">
        <div className="card-body">
          <h5 className="card-title">Authorization Status</h5>
          
          {!isAuthorized ? (
            <div>
              <p className="text-warning">⚠️ Not connected to ANAF eFactura</p>
              <button 
                className="btn btn-primary" 
                onClick={handleAuthorize} 
                disabled={loading}
              >
                {loading ? 'Connecting...' : '🔗 Connect to eFactura'}
              </button>
            </div>
          ) : (
            <div>
              <p className="text-success">✅ Connected to ANAF eFactura</p>
              
              <div className="mt-3">
                <h6>Get Invoices</h6>
                <div className="input-group mb-3">
                  <input
                    type="text"
                    className="form-control"
                    placeholder="Enter CUI"
                    value={cui}
                    onChange={(e) => setCui(e.target.value)}
                  />
                  <button
                    className="btn btn-success"
                    onClick={handleGetInvoices}
                    disabled={loading}
                  >
                    {loading ? 'Loading...' : '📄 Get Invoices'}
                  </button>
                </div>
              </div>
            </div>
          )}
        </div>
      </div>

      {invoices && (
        <div className="card mt-3">
          <div className="card-body">
            <h5 className="card-title">Invoice Data</h5>
            <pre className="bg-light p-3 rounded">
              <code>{invoices}</code>
            </pre>
          </div>
        </div>
      )}
    </div>
  );
};

export default EFacturaIntegration;
```

### 🖥️ Console Application Example

**Program.cs**
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RoEFactura;
using RoEFactura.Services.Authentication;

namespace EFacturaConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Setup hosting and DI
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices(services =>
                {
                    services.AddRoEFactura();
                })
                .Build();

            var anafClient = host.Services.GetRequiredService<IAnafOAuthClient>();
            var invoiceClient = host.Services.GetRequiredService<IAnafEInvoiceClient>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                Console.WriteLine("🇷🇴 RoEFactura Console Application");
                Console.WriteLine("====================================");
                
                // Get configuration from user
                Console.Write("Enter ANAF Client ID: ");
                var clientId = Console.ReadLine();
                
                Console.Write("Enter ANAF Client Secret: ");
                var clientSecret = ReadPassword();
                
                Console.Write("Enter CUI to query: ");
                var cui = Console.ReadLine();

                Console.WriteLine("\n🔐 Authenticating with ANAF...");

                // Authenticate using certificate
                var token = await anafClient.GetAccessTokenAsync(
                    clientId,
                    clientSecret, 
                    "https://localhost/callback"
                );

                Console.WriteLine("✅ Authentication successful!");
                Console.WriteLine($"Token expires: {DateTime.UtcNow.AddSeconds(token.ExpiresIn)}");

                // Get invoices
                Console.WriteLine("\n📄 Retrieving invoices...");
                var invoices = await invoiceClient.ListEInvoicesAsync(token.AccessToken, 30, cui);
                
                Console.WriteLine("✅ Invoices retrieved!");
                Console.WriteLine("\nInvoice Data:");
                Console.WriteLine(invoices);

            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Application failed");
                Console.WriteLine($"❌ Error: {ex.Message}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner error: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, password.Length - 1);
                    Console.Write("\b \b");
                }
            }
            while (key.Key != ConsoleKey.Enter);
            
            Console.WriteLine();
            return password;
        }
    }
}
```

---

## 🔧 Advanced Usage

### Custom Certificate Selection UI

```csharp
public class CertificateSelectionService
{
    public async Task<Token> AuthenticateWithUserSelectedCertificate(
        string clientId, string clientSecret, string callbackUrl)
    {
        // Get available certificates
        var certificates = AnafOAuthClient.GetAvailableRomanianCertificates();
        
        if (!certificates.Any())
        {
            throw new InvalidOperationException("No Romanian certificates found");
        }

        // Display certificates to user
        Console.WriteLine("Available Certificates:");
        for (int i = 0; i < certificates.Count; i++)
        {
            var cert = certificates[i];
            Console.WriteLine($"{i + 1}. {GetCertificateDisplayName(cert)}");
            Console.WriteLine($"   Issuer: {cert.Issuer}");
            Console.WriteLine($"   Expires: {cert.ExpiryDate:yyyy-MM-dd}");
            Console.WriteLine($"   Valid for auth: {cert.IsValidForClientAuth}");
            Console.WriteLine();
        }

        // Get user selection
        Console.Write("Select certificate (enter number): ");
        if (int.TryParse(Console.ReadLine(), out int selection) && 
            selection > 0 && selection <= certificates.Count)
        {
            var selectedCert = certificates[selection - 1];
            
            var anafClient = new AnafOAuthClient(/* your HttpClientFactory */);
            return await anafClient.GetAccessTokenAsync(
                selectedCert.Thumbprint, clientId, clientSecret, callbackUrl);
        }

        throw new InvalidOperationException("Invalid certificate selection");
    }

    private string GetCertificateDisplayName(CertificateInfo cert)
    {
        // Extract common name from subject
        var subject = cert.Subject;
        var cnMatch = System.Text.RegularExpressions.Regex.Match(subject, @"CN=([^,]+)");
        return cnMatch.Success ? cnMatch.Groups[1].Value : subject;
    }
}
```

### Token Management and Refresh

```csharp
public class TokenManager
{
    private readonly ILogger<TokenManager> _logger;
    private Timer _refreshTimer;
    private Token _currentToken;
    private readonly object _tokenLock = new object();

    public event EventHandler<Token> TokenRefreshed;

    public TokenManager(ILogger<TokenManager> logger)
    {
        _logger = logger;
    }

    public void SetToken(Token token)
    {
        lock (_tokenLock)
        {
            _currentToken = token;
            
            // Setup auto-refresh 5 minutes before expiry
            var refreshTime = TimeSpan.FromSeconds(token.ExpiresIn - 300);
            _refreshTimer?.Dispose();
            _refreshTimer = new Timer(RefreshTokenCallback, null, refreshTime, Timeout.InfiniteTimeSpan);
            
            _logger.LogInformation("Token set, auto-refresh scheduled for {RefreshTime}", 
                DateTime.UtcNow.Add(refreshTime));
        }
    }

    public Token GetValidToken()
    {
        lock (_tokenLock)
        {
            if (_currentToken == null)
                throw new InvalidOperationException("No token available");

            // Check if token is expired (with 1 minute buffer)
            var expiryTime = DateTime.UtcNow.AddSeconds(_currentToken.ExpiresIn);
            if (expiryTime <= DateTime.UtcNow.AddMinutes(1))
            {
                _logger.LogWarning("Token is expired or about to expire");
                return null;
            }

            return _currentToken;
        }
    }

    private async void RefreshTokenCallback(object state)
    {
        try
        {
            _logger.LogInformation("Attempting to refresh token...");
            
            // Implement token refresh logic here
            // This depends on whether ANAF supports refresh tokens
            // or if you need to re-authenticate
            
            _logger.LogInformation("Token refreshed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh failed");
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
```

### Error Handling Best Practices

```csharp
public class EFacturaService
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly IAnafEInvoiceClient _invoiceClient;
    private readonly ILogger<EFacturaService> _logger;

    public async Task<ServiceResult<List<Invoice>>> GetInvoicesWithRetry(
        string accessToken, string cui, int days, int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempting to get invoices (attempt {Attempt}/{MaxRetries})", 
                    attempt, maxRetries);

                var result = await _invoiceClient.ListEInvoicesAsync(accessToken, days, cui);
                
                // Parse and validate result
                var invoices = ParseInvoiceResponse(result);
                
                return ServiceResult<List<Invoice>>.Success(invoices);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("401"))
            {
                _logger.LogWarning("Access token expired, need re-authentication");
                return ServiceResult<List<Invoice>>.Failure("TOKEN_EXPIRED", 
                    "Access token has expired. Please re-authenticate.");
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                _logger.LogWarning("Rate limit exceeded, waiting before retry...");
                if (attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
                    continue;
                }
                
                return ServiceResult<List<Invoice>>.Failure("RATE_LIMIT", 
                    "Rate limit exceeded. Please try again later.");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("certificate"))
            {
                _logger.LogError(ex, "Certificate authentication failed");
                return ServiceResult<List<Invoice>>.Failure("CERT_ERROR", 
                    $"Certificate authentication failed: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting invoices (attempt {Attempt})", attempt);
                
                if (attempt == maxRetries)
                {
                    return ServiceResult<List<Invoice>>.Failure("UNKNOWN_ERROR", 
                        $"Failed after {maxRetries} attempts: {ex.Message}");
                }
                
                // Wait before retry
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
            }
        }

        return ServiceResult<List<Invoice>>.Failure("MAX_RETRIES", 
            $"Operation failed after {maxRetries} attempts");
    }

    private List<Invoice> ParseInvoiceResponse(string jsonResponse)
    {
        try
        {
            // Implement your parsing logic here
            var data = System.Text.Json.JsonSerializer.Deserialize<InvoiceResponse>(jsonResponse);
            return data?.Invoices ?? new List<Invoice>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse invoice response");
            throw new InvalidOperationException("Invalid response format from ANAF API", ex);
        }
    }
}

public class ServiceResult<T>
{
    public bool IsSuccess { get; set; }
    public T Data { get; set; }
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }

    public static ServiceResult<T> Success(T data) => 
        new ServiceResult<T> { IsSuccess = true, Data = data };

    public static ServiceResult<T> Failure(string errorCode, string errorMessage) => 
        new ServiceResult<T> 
        { 
            IsSuccess = false, 
            ErrorCode = errorCode, 
            ErrorMessage = errorMessage 
        };
}
```

### Multi-tenant Support

```csharp
public class MultiTenantEFacturaService
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly ITenantResolver _tenantResolver;
    private readonly IOptionsSnapshot<AnafOAuthOptions> _oauthOptions;
    private readonly ILogger<MultiTenantEFacturaService> _logger;

    public MultiTenantEFacturaService(
        IAnafOAuthClient anafClient,
        ITenantResolver tenantResolver,
        IOptionsSnapshot<AnafOAuthOptions> oauthOptions,
        ILogger<MultiTenantEFacturaService> logger)
    {
        _anafClient = anafClient;
        _tenantResolver = tenantResolver;
        _oauthOptions = oauthOptions;
        _logger = logger;
    }

    public async Task<string> InitiateOAuthForTenant(string tenantId)
    {
        var tenant = await _tenantResolver.GetTenantAsync(tenantId);
        if (tenant == null)
            throw new ArgumentException($"Tenant {tenantId} not found");

        // Get tenant-specific OAuth options
        var options = _oauthOptions.Get(tenantId);
        
        // Generate tenant-specific state
        var state = GenerateStateWithTenant(tenantId);
        
        // Store state with tenant context
        await StoreStateForTenant(tenantId, state);

        var authUrl = _anafClient.GenerateAuthorizationUrl(options, state);
        
        _logger.LogInformation("OAuth initiated for tenant {TenantId}", tenantId);
        
        return authUrl;
    }

    public async Task<Token> HandleCallbackForTenant(string code, string state)
    {
        // Extract tenant ID from state
        var tenantId = ExtractTenantFromState(state);
        
        // Validate state for tenant
        if (!await ValidateStateForTenant(tenantId, state))
            throw new UnauthorizedAccessException("Invalid state parameter");

        // Get tenant-specific options
        var options = _oauthOptions.Get(tenantId);
        
        // Exchange code for token
        var token = await _anafClient.ExchangeAuthorizationCodeAsync(code, options);
        
        // Store token for tenant
        await StoreTokenForTenant(tenantId, token);
        
        _logger.LogInformation("OAuth completed for tenant {TenantId}", tenantId);
        
        return token;
    }

    private string GenerateStateWithTenant(string tenantId)
    {
        var stateData = new { TenantId = tenantId, Nonce = Guid.NewGuid().ToString() };
        var json = System.Text.Json.JsonSerializer.Serialize(stateData);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private string ExtractTenantFromState(string state)
    {
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(state));
            var stateData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);
            return stateData.TenantId;
        }
        catch
        {
            throw new ArgumentException("Invalid state format");
        }
    }
}
```

---

## 📖 API Reference

### IAnafOAuthClient Interface

#### Certificate-Based Authentication Methods

```csharp
/// <summary>
/// Authenticates with ANAF using automatically discovered Romanian certificate
/// </summary>
/// <param name="clientId">ANAF OAuth client ID</param>
/// <param name="clientSecret">ANAF OAuth client secret</param>
/// <param name="callbackUrl">OAuth callback URL</param>
/// <returns>Access token for ANAF API calls</returns>
/// <exception cref="InvalidOperationException">Thrown when no certificate found or multiple certificates detected</exception>
Task<Token> GetAccessTokenAsync(string clientId, string clientSecret, string callbackUrl);

/// <summary>
/// Authenticates with ANAF using the specified certificate
/// </summary>
/// <param name="certificate">X509Certificate2 object to use for authentication</param>
/// <param name="clientId">ANAF OAuth client ID</param>
/// <param name="clientSecret">ANAF OAuth client secret</param>
/// <param name="callbackUrl">OAuth callback URL</param>
/// <returns>Access token for ANAF API calls</returns>
Task<Token> GetAccessTokenAsync(X509Certificate2 certificate, string clientId, string clientSecret, string callbackUrl);

/// <summary>
/// Authenticates with ANAF using certificate identified by thumbprint
/// </summary>
/// <param name="thumbprint">Certificate thumbprint (SHA-1 hash)</param>
/// <param name="clientId">ANAF OAuth client ID</param>
/// <param name="clientSecret">ANAF OAuth client secret</param>
/// <param name="callbackUrl">OAuth callback URL</param>
/// <returns>Access token for ANAF API calls</returns>
/// <exception cref="InvalidOperationException">Thrown when certificate with specified thumbprint not found</exception>
Task<Token> GetAccessTokenAsync(string thumbprint, string clientId, string clientSecret, string callbackUrl);
```

#### OAuth Redirect Flow Methods

```csharp
/// <summary>
/// Generates the OAuth authorization URL for redirecting users to ANAF
/// </summary>
/// <param name="clientId">OAuth client ID</param>
/// <param name="redirectUri">Redirect URI (must be registered with ANAF)</param>
/// <param name="state">Optional state parameter for CSRF protection</param>
/// <returns>The authorization URL to redirect the user to</returns>
string GenerateAuthorizationUrl(string clientId, string redirectUri, string? state = null);

/// <summary>
/// Generates the OAuth authorization URL using configured options
/// </summary>
/// <param name="options">OAuth configuration options</param>
/// <param name="state">Optional state parameter for CSRF protection</param>
/// <returns>The authorization URL to redirect the user to</returns>
/// <exception cref="ArgumentException">Thrown when options are invalid</exception>
string GenerateAuthorizationUrl(AnafOAuthOptions options, string? state = null);

/// <summary>
/// Exchanges an authorization code for access token
/// </summary>
/// <param name="code">Authorization code received from ANAF callback</param>
/// <param name="clientId">OAuth client ID</param>
/// <param name="clientSecret">OAuth client secret</param>
/// <param name="redirectUri">Redirect URI (must match the one used in authorization)</param>
/// <returns>Access token for ANAF API calls</returns>
/// <exception cref="InvalidOperationException">Thrown when token exchange fails</exception>
Task<Token> ExchangeAuthorizationCodeAsync(string code, string clientId, string clientSecret, string redirectUri);

/// <summary>
/// Exchanges an authorization code for access token using configured options
/// </summary>
/// <param name="code">Authorization code received from ANAF callback</param>
/// <param name="options">OAuth configuration options</param>
/// <returns>Access token for ANAF API calls</returns>
/// <exception cref="ArgumentException">Thrown when options are invalid</exception>
/// <exception cref="InvalidOperationException">Thrown when token exchange fails</exception>
Task<Token> ExchangeAuthorizationCodeAsync(string code, AnafOAuthOptions options);
```

### AnafOAuthOptions Class

```csharp
public class AnafOAuthOptions
{
    /// <summary>
    /// ANAF OAuth Client ID (Required)
    /// </summary>
    public string ClientId { get; set; } = string.Empty;
    
    /// <summary>
    /// ANAF OAuth Client Secret (Required)
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;
    
    /// <summary>
    /// OAuth redirect URI - must be registered with ANAF (Required)
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
    
    /// <summary>
    /// ANAF authorization endpoint URL
    /// Default: https://logincert.anaf.ro/anaf-oauth2/v1/authorize
    /// </summary>
    public string AuthorizeUrl { get; set; } = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize";
    
    /// <summary>
    /// ANAF token endpoint URL
    /// Default: https://logincert.anaf.ro/anaf-oauth2/v1/token
    /// </summary>
    public string TokenUrl { get; set; } = "https://logincert.anaf.ro/anaf-oauth2/v1/token";
    
    /// <summary>
    /// Whether to include token_content_type=jwt parameter
    /// Default: true (following SmartBill pattern)
    /// </summary>
    public bool IncludeTokenContentType { get; set; } = true;
    
    /// <summary>
    /// Validates the configuration
    /// </summary>
    /// <returns>True if all required properties are set</returns>
    public bool IsValid();
}
```

### Token Class

```csharp
public class Token
{
    /// <summary>
    /// JWT access token for ANAF API calls
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Refresh token (if provided by ANAF)
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;
    
    /// <summary>
    /// Token expiration time in seconds
    /// </summary>
    public int ExpiresIn { get; set; }
    
    /// <summary>
    /// Token type (usually "Bearer")
    /// </summary>
    public string TokenType { get; set; } = "Bearer";
    
    /// <summary>
    /// Token scope (if provided by ANAF)
    /// </summary>
    public string Scope { get; set; } = string.Empty;
}
```

### Service Registration Extensions

```csharp
/// <summary>
/// Adds RoEFactura services to the DI container
/// </summary>
/// <param name="services">The service collection</param>
/// <param name="configuration">Optional configuration</param>
/// <returns>The service collection for chaining</returns>
public static IServiceCollection AddRoEFactura(this IServiceCollection services, IConfiguration? configuration = null);

/// <summary>
/// Adds RoEFactura services with OAuth configuration
/// </summary>
/// <param name="services">The service collection</param>
/// <param name="oauthOptions">OAuth configuration options</param>
/// <param name="configuration">Optional additional configuration</param>
/// <returns>The service collection for chaining</returns>
public static IServiceCollection AddRoEFacturaWithOAuth(this IServiceCollection services, 
    AnafOAuthOptions oauthOptions, 
    IConfiguration? configuration = null);

/// <summary>
/// Adds RoEFactura services with OAuth configuration from IConfiguration
/// </summary>
/// <param name="services">The service collection</param>
/// <param name="configuration">Configuration containing OAuth settings</param>
/// <param name="sectionName">Configuration section name (defaults to "AnafOAuth")</param>
/// <returns>The service collection for chaining</returns>
public static IServiceCollection AddRoEFacturaWithOAuth(this IServiceCollection services,
    IConfiguration configuration,
    string sectionName = "AnafOAuth");
```

---

## 🧪 Testing & Debugging

### Local Testing Setup

#### 1. Test Certificate Installation

```csharp
public class CertificateTestService
{
    public void TestCertificateSetup()
    {
        try
        {
            // Check available certificates
            var certificates = AnafOAuthClient.GetAvailableRomanianCertificates();
            
            Console.WriteLine($"Found {certificates.Count} Romanian certificates:");
            
            foreach (var cert in certificates)
            {
                Console.WriteLine($"✓ Subject: {cert.Subject}");
                Console.WriteLine($"  Issuer: {cert.Issuer}");
                Console.WriteLine($"  Thumbprint: {cert.Thumbprint}");
                Console.WriteLine($"  Expires: {cert.ExpiryDate:yyyy-MM-dd}");
                Console.WriteLine($"  Has Private Key: {cert.HasPrivateKey}");
                Console.WriteLine($"  Valid for Client Auth: {cert.IsValidForClientAuth}");
                Console.WriteLine($"  Status: {GetCertificateStatus(cert)}");
                Console.WriteLine();
            }

            if (certificates.Any(c => c.IsValidForClientAuth))
            {
                Console.WriteLine("✅ Certificate setup is correct!");
            }
            else
            {
                Console.WriteLine("❌ No valid certificates found for client authentication");
                Console.WriteLine("Please install a Romanian digital certificate from:");
                Console.WriteLine("- CERTSIGN: https://www.certsign.ro/");
                Console.WriteLine("- DIGISIGN: https://www.digisign.ro/");
                Console.WriteLine("- ALFASIGN: https://www.alfatrust.ro/");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Certificate test failed: {ex.Message}");
        }
    }

    private string GetCertificateStatus(CertificateInfo cert)
    {
        if (cert.ExpiryDate < DateTime.Now)
            return "❌ EXPIRED";
        
        if (!cert.HasPrivateKey)
            return "⚠️ No Private Key";
            
        if (!cert.IsValidForClientAuth)
            return "⚠️ Invalid for Client Auth";
            
        if (cert.ExpiryDate < DateTime.Now.AddDays(30))
            return "⚠️ Expires Soon";
            
        return "✅ Valid";
    }
}
```

#### 2. OAuth Flow Testing

```csharp
public class OAuthFlowTester
{
    private readonly IAnafOAuthClient _anafClient;

    public async Task TestOAuthFlow()
    {
        var options = new AnafOAuthOptions
        {
            ClientId = "your_test_client_id",
            ClientSecret = "your_test_client_secret",
            RedirectUri = "http://localhost:8080/callback"
        };

        try
        {
            // Step 1: Test URL generation
            Console.WriteLine("📋 Step 1: Testing authorization URL generation");
            var state = Guid.NewGuid().ToString();
            var authUrl = _anafClient.GenerateAuthorizationUrl(options, state);
            Console.WriteLine($"✅ Generated URL: {authUrl}");

            // Step 2: Simulate user flow (manual)
            Console.WriteLine("\n📋 Step 2: Manual OAuth flow test");
            Console.WriteLine("1. Open the following URL in your browser:");
            Console.WriteLine(authUrl);
            Console.WriteLine("2. Complete the OAuth flow");
            Console.WriteLine("3. Copy the 'code' parameter from the callback URL");
            Console.Write("Enter the authorization code: ");
            var code = Console.ReadLine();

            if (!string.IsNullOrEmpty(code))
            {
                // Step 3: Test token exchange
                Console.WriteLine("\n📋 Step 3: Testing token exchange");
                var token = await _anafClient.ExchangeAuthorizationCodeAsync(code, options);
                
                Console.WriteLine("✅ Token exchange successful!");
                Console.WriteLine($"Access Token: {token.AccessToken.Substring(0, 20)}...");
                Console.WriteLine($"Token Type: {token.TokenType}");
                Console.WriteLine($"Expires In: {token.ExpiresIn} seconds");
                Console.WriteLine($"Scope: {token.Scope}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ OAuth flow test failed: {ex.Message}");
            
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner error: {ex.InnerException.Message}");
            }
        }
    }
}
```

### Common Issues and Solutions

#### Issue 1: "No valid certificates found"

**Symptoms:**
```
InvalidOperationException: No valid Romanian certificates found for client authentication.
```

**Solutions:**
1. **Install Romanian digital certificate:**
   - Download from CERTSIGN, DIGISIGN, or ALFASIGN
   - Install in Windows Certificate Store (CurrentUser/Personal)
   - Ensure certificate has private key

2. **Check certificate validity:**
   ```csharp
   var certs = AnafOAuthClient.GetAvailableRomanianCertificates();
   foreach (var cert in certs)
   {
       Console.WriteLine($"Cert: {cert.Subject}");
       Console.WriteLine($"Valid: {cert.IsValidForClientAuth}");
       Console.WriteLine($"Has Key: {cert.HasPrivateKey}");
       Console.WriteLine($"Expires: {cert.ExpiryDate}");
   }
   ```

#### Issue 2: "Multiple certificates found"

**Symptoms:**
```
InvalidOperationException: Multiple valid certificates found (2). Please ensure only one Romanian certificate for client authentication is installed.
```

**Solution:**
Use specific certificate by thumbprint:
```csharp
var certs = AnafOAuthClient.GetAvailableRomanianCertificates();
var selectedCert = certs.First(c => c.IsValidForClientAuth);

var token = await anafClient.GetAccessTokenAsync(
    selectedCert.Thumbprint, clientId, clientSecret, callbackUrl);
```

#### Issue 3: "Token exchange failed"

**Symptoms:**
```
InvalidOperationException: Token exchange failed. HTTP Status: 400. Response: {"error":"invalid_client"}
```

**Solutions:**
1. **Verify client credentials:**
   - Check ClientId and ClientSecret are correct
   - Ensure they're registered with ANAF
   
2. **Check redirect URI:**
   - Must exactly match the one registered with ANAF
   - Include protocol (http/https)
   - No trailing slashes unless registered that way

3. **Verify authorization code:**
   - Code is single-use only
   - Must be used within ~10 minutes
   - Cannot be reused

#### Issue 4: OAuth callback not working

**Symptoms:**
- Browser shows "This site can't be reached" after OAuth
- Callback never gets called

**Solutions:**
1. **Local development setup:**
   ```csharp
   // For testing, use localhost with specific port
   var options = new AnafOAuthOptions
   {
       RedirectUri = "http://localhost:5000/api/oauth/callback"
   };
   ```

2. **Ensure callback endpoint exists:**
   ```csharp
   [HttpGet("oauth/callback")]
   public async Task<IActionResult> Callback(string code, string state)
   {
       // Handle callback
   }
   ```

3. **Register callback URL with ANAF:**
   - Exact URL must be whitelisted
   - Include port number for localhost
   - Use http:// for local testing

---

## 🚀 Production Deployment

### ANAF Client Registration

#### Step 1: Register OAuth Application

1. **Access ANAF Developer Portal:**
   - Visit: https://www.anaf.ro/dezvoltatori/
   - Login with your Romanian digital certificate

2. **Create OAuth Application:**
   - Application Name: Your company/app name
   - Application Type: Web Application
   - Redirect URLs: Your production callback URLs
   - Scopes: Select required permissions

3. **Get Credentials:**
   - Client ID: Unique identifier for your app
   - Client Secret: Secret key for token exchange
   - Save these securely!

#### Step 2: Configure Production URLs

```csharp
// Production configuration
services.AddRoEFacturaWithOAuth(new AnafOAuthOptions
{
    ClientId = configuration["ANAF_CLIENT_ID"], // From environment
    ClientSecret = configuration["ANAF_CLIENT_SECRET"], // From environment  
    RedirectUri = "https://yourdomain.com/api/oauth/anaf/callback",
    AuthorizeUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/authorize",
    TokenUrl = "https://logincert.anaf.ro/anaf-oauth2/v1/token"
});
```

### Certificate Management in Production

#### Server Certificate Installation

```csharp
public class ProductionCertificateService
{
    public async Task<Token> AuthenticateInProduction(string clientId, string clientSecret)
    {
        try
        {
            // In production, use certificate store or Azure Key Vault
            var certificate = await LoadCertificateFromSecureStore();
            
            var anafClient = serviceProvider.GetService<IAnafOAuthClient>();
            return await anafClient.GetAccessTokenAsync(
                certificate, clientId, clientSecret, "production_callback_url");
        }
        catch (Exception ex)
        {
            // Log error and implement fallback
            _logger.LogError(ex, "Production authentication failed");
            throw;
        }
    }

    private async Task<X509Certificate2> LoadCertificateFromSecureStore()
    {
        // Option 1: Azure Key Vault
        var keyVaultClient = new KeyVaultClient(/* credentials */);
        var certificateBundle = await keyVaultClient.GetCertificateAsync(
            "https://yourvault.vault.azure.net/", "anaf-certificate");
        
        return new X509Certificate2(certificateBundle.Cer);

        // Option 2: Windows Certificate Store (on Windows servers)
        // using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
        // {
        //     store.Open(OpenFlags.ReadOnly);
        //     var certificates = store.Certificates.Find(
        //         X509FindType.FindByThumbprint, "your_cert_thumbprint", false);
        //     return certificates[0];
        // }
    }
}
```

### Security Considerations

#### 1. Secure Token Storage

```csharp
public class SecureTokenService
{
    private readonly IDataProtector _protector;

    public SecureTokenService(IDataProtectionProvider dataProtection)
    {
        _protector = dataProtection.CreateProtector("AnafTokens");
    }

    public async Task StoreTokenAsync(string userId, Token token)
    {
        var tokenData = new
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn),
            TokenType = token.TokenType,
            Scope = token.Scope
        };

        var serialized = JsonSerializer.Serialize(tokenData);
        var encrypted = _protector.Protect(serialized);
        
        // Store encrypted token in database
        await _database.SaveTokenAsync(userId, encrypted);
    }

    public async Task<Token> GetTokenAsync(string userId)
    {
        var encrypted = await _database.GetTokenAsync(userId);
        if (string.IsNullOrEmpty(encrypted)) return null;

        var decrypted = _protector.Unprotect(encrypted);
        var tokenData = JsonSerializer.Deserialize<dynamic>(decrypted);
        
        // Check if token is expired
        var expiresAt = DateTime.Parse(tokenData.ExpiresAt.ToString());
        if (expiresAt <= DateTime.UtcNow)
        {
            await _database.DeleteTokenAsync(userId);
            return null;
        }

        return new Token
        {
            AccessToken = tokenData.AccessToken,
            RefreshToken = tokenData.RefreshToken,
            // Calculate remaining time
            ExpiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds,
            TokenType = tokenData.TokenType,
            Scope = tokenData.Scope
        };
    }
}
```

#### 2. Rate Limiting and Resilience

```csharp
public class ResilientAnafService
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly ILogger<ResilientAnafService> _logger;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent

    public async Task<Token> GetTokenWithRetry(
        string clientId, string clientSecret, string callbackUrl)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await _semaphore.WaitAsync();
            
            try
            {
                return await _anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                _logger.LogWarning("Rate limited on attempt {Attempt}, waiting...", attempt);
                
                if (attempt < maxRetries)
                {
                    var delay = TimeSpan.FromMilliseconds(baseDelayMs * Math.Pow(2, attempt - 1));
                    await Task.Delay(delay);
                    continue;
                }
                
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Authentication failed on attempt {Attempt}", attempt);
                
                if (attempt == maxRetries) throw;
                
                await Task.Delay(TimeSpan.FromMilliseconds(baseDelayMs * attempt));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        throw new InvalidOperationException($"Authentication failed after {maxRetries} attempts");
    }
}
```

#### 3. Monitoring and Logging

```csharp
public class AnafServiceWithMonitoring
{
    private readonly IAnafOAuthClient _anafClient;
    private readonly ILogger<AnafServiceWithMonitoring> _logger;
    private readonly IMetrics _metrics;

    public async Task<Token> GetTokenWithMonitoring(
        string clientId, string clientSecret, string callbackUrl)
    {
        using var activity = Activity.StartActivity("AnafAuthentication");
        activity?.SetTag("client_id", clientId);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Starting ANAF authentication for client {ClientId}", clientId);
            
            var token = await _anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "ANAF authentication successful for client {ClientId} in {Duration}ms", 
                clientId, stopwatch.ElapsedMilliseconds);
            
            // Record metrics
            _metrics.Measure.Counter.Increment("anaf_auth_success");
            _metrics.Measure.Timer.Time("anaf_auth_duration", stopwatch.ElapsedMilliseconds);
            
            activity?.SetTag("success", "true");
            activity?.SetTag("expires_in", token.ExpiresIn.ToString());
            
            return token;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex, 
                "ANAF authentication failed for client {ClientId} after {Duration}ms", 
                clientId, stopwatch.ElapsedMilliseconds);
            
            _metrics.Measure.Counter.Increment("anaf_auth_failure", 
                new MetricTags("error", ex.GetType().Name));
            
            activity?.SetTag("success", "false");
            activity?.SetTag("error", ex.Message);
            
            throw;
        }
    }
}
```

### Performance Optimization

#### 1. Token Caching

```csharp
public class CachedTokenService
{
    private readonly IMemoryCache _cache;
    private readonly IAnafOAuthClient _anafClient;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public async Task<Token> GetCachedTokenAsync(string clientId, string clientSecret, string callbackUrl)
    {
        var cacheKey = $"anaf_token_{clientId}";
        
        // Try to get from cache first
        if (_cache.TryGetValue(cacheKey, out Token cachedToken))
        {
            // Check if token expires in the next 5 minutes
            var expiryTime = DateTime.UtcNow.AddSeconds(cachedToken.ExpiresIn);
            if (expiryTime > DateTime.UtcNow.AddMinutes(5))
            {
                return cachedToken;
            }
        }

        // Token not in cache or expired, get new one
        await _semaphore.WaitAsync();
        
        try
        {
            // Double-check cache in case another thread got the token
            if (_cache.TryGetValue(cacheKey, out cachedToken))
            {
                var expiryTime = DateTime.UtcNow.AddSeconds(cachedToken.ExpiresIn);
                if (expiryTime > DateTime.UtcNow.AddMinutes(5))
                {
                    return cachedToken;
                }
            }

            // Get new token
            var newToken = await _anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
            
            // Cache with expiration 5 minutes before token expires
            var cacheExpiry = TimeSpan.FromSeconds(newToken.ExpiresIn - 300);
            _cache.Set(cacheKey, newToken, cacheExpiry);
            
            return newToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

#### 2. Connection Pooling

```csharp
// In Program.cs/Startup.cs
services.AddHttpClient<IAnafOAuthClient, AnafOAuthClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "YourApp/1.0");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxConnectionsPerServer = 10,
    PooledConnectionLifetime = TimeSpan.FromMinutes(15)
});
```

---

## 🔄 Migration Guide

### From Version 1.x to 2.x

#### Breaking Changes

1. **New OAuth Methods Added:**
   - `IAnafOAuthClient` now includes OAuth redirect methods
   - No breaking changes to existing certificate methods

2. **New Dependencies:**
   - No new required dependencies
   - OAuth functionality uses existing `IHttpClientFactory`

#### Migration Steps

**Step 1: Update NuGet Package**
```bash
dotnet add package RoEFactura --version 2.0.0
```

**Step 2: Update Service Registration (Optional)**

If you want to use OAuth features:
```csharp
// Before (still works)
services.AddRoEFactura();

// After (with OAuth support)
services.AddRoEFacturaWithOAuth(configuration);
```

**Step 3: No Code Changes Required**

Existing certificate-based code continues to work unchanged:
```csharp
// This code still works exactly the same
var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
```

#### New OAuth Features

Add OAuth support for web applications:
```csharp
// New OAuth redirect functionality
var authUrl = anafClient.GenerateAuthorizationUrl(options, state);
var token = await anafClient.ExchangeAuthorizationCodeAsync(code, options);
```

### Upgrading from Custom Implementation

If you're currently using a custom ANAF integration:

#### Step 1: Install RoEFactura
```bash
dotnet add package RoEFactura
```

#### Step 2: Replace Certificate Loading
```csharp
// Before: Custom certificate loading
var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadOnly);
var certificates = store.Certificates.Find(X509FindType.FindByIssuerName, "CERTSIGN", false);
var certificate = certificates[0];

// After: Use RoEFactura auto-discovery
services.AddRoEFactura();
var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
```

#### Step 3: Replace HTTP Client Code
```csharp
// Before: Manual HttpClient with certificate
var handler = new HttpClientHandler();
handler.ClientCertificates.Add(certificate);
var client = new HttpClient(handler);

// After: RoEFactura handles everything
var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
```

#### Step 4: Replace Token Parsing
```csharp
// Before: Manual JSON parsing
var response = await client.PostAsync(tokenUrl, content);
var json = await response.Content.ReadAsStringAsync();
var tokenData = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

// After: Strongly-typed response
var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
// token.AccessToken, token.ExpiresIn, etc. are all available
```

---

## ❓ Frequently Asked Questions

### General Questions

**Q: What is RoEFactura?**
A: RoEFactura is a .NET library that simplifies integration with the Romanian ANAF eInvoicing system. It handles certificate authentication, OAuth flows, and API calls to ANAF's electronic invoice services.

**Q: Which .NET versions are supported?**
A: RoEFactura supports .NET 9.0. For older versions, please use RoEFactura v1.x which supports .NET 6.0+.

**Q: Is this library official from ANAF?**
A: No, this is a community-developed library. It uses ANAF's official APIs but is not created or endorsed by ANAF.

### Authentication Questions

**Q: What's the difference between certificate and OAuth authentication?**
A: 
- **Certificate authentication**: Uses Romanian digital certificates installed on the machine. Best for desktop apps and server-to-server integration.
- **OAuth authentication**: Uses browser-based login with certificate selection. Best for web applications where users authenticate themselves.

**Q: Where do I get Romanian digital certificates?**
A: You can obtain certificates from authorized Romanian Certificate Authorities:
- **CERTSIGN**: https://www.certsign.ro/
- **DIGISIGN**: https://www.digisign.ro/
- **ALFASIGN**: https://www.alfatrust.ro/

**Q: Can I use the same certificate for multiple applications?**
A: Yes, but each application needs its own ANAF OAuth client credentials (ClientId and ClientSecret).

### Configuration Questions

**Q: How do I get ANAF OAuth client credentials?**
A: 
1. Visit ANAF's developer portal
2. Register your application  
3. Provide redirect URLs
4. Receive ClientId and ClientSecret
5. Keep these credentials secure!

**Q: Can I use localhost URLs for development?**
A: Yes, ANAF allows localhost URLs for development. Register URLs like `http://localhost:5000/api/oauth/callback`.

**Q: What redirect URLs should I register?**
A: Register the exact URLs where your application will handle OAuth callbacks:
- Development: `http://localhost:5000/api/oauth/callback`
- Production: `https://yourdomain.com/api/oauth/callback`

### Integration Questions

**Q: How do I handle token expiration?**
A: Tokens typically expire after 1 hour. Store the `ExpiresIn` value and refresh before expiration:
```csharp
if (DateTime.UtcNow.AddSeconds(token.ExpiresIn) <= DateTime.UtcNow.AddMinutes(5))
{
    // Token expires in 5 minutes, get a new one
    token = await GetNewToken();
}
```

**Q: Can I use this in a multi-tenant application?**
A: Yes! Each tenant can have their own OAuth credentials. Use `IOptionsSnapshot<AnafOAuthOptions>` for per-tenant configuration.

**Q: How do I handle errors gracefully?**
A: Implement retry logic and proper error handling:
```csharp
try 
{
    var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("certificate"))
{
    // Handle certificate issues
}
catch (HttpRequestException ex) when (ex.Message.Contains("401"))
{
    // Handle authentication issues  
}
```

### Production Questions

**Q: How should I store tokens in production?**
A: Use secure storage:
- Encrypt tokens using ASP.NET Core Data Protection
- Store in secure databases (not session/cookies for sensitive data)
- Use Azure Key Vault or similar for certificate storage

**Q: What about rate limiting?**
A: ANAF APIs have rate limits. Implement:
- Retry logic with exponential backoff
- Request throttling
- Token caching to reduce authentication calls

**Q: How do I monitor the integration?**
A: Add logging and metrics:
```csharp
_logger.LogInformation("ANAF authentication successful for {ClientId}", clientId);
_metrics.Measure.Counter.Increment("anaf_auth_success");
```

### Troubleshooting Questions

**Q: "No valid certificates found" error?**
A: 
1. Install a Romanian digital certificate
2. Ensure it's in CurrentUser/Personal store
3. Verify it has a private key
4. Check certificate hasn't expired

**Q: "Multiple certificates found" error?**
A: Use specific certificate selection:
```csharp
var certs = AnafOAuthClient.GetAvailableRomanianCertificates();
var selectedCert = certs.First(c => c.IsValidForClientAuth);
var token = await anafClient.GetAccessTokenAsync(selectedCert.Thumbprint, clientId, clientSecret, callbackUrl);
```

**Q: "Token exchange failed" error?**
A: Check:
- ClientId and ClientSecret are correct
- Redirect URI exactly matches registration
- Authorization code hasn't expired (use within ~10 minutes)
- Code hasn't been used before (single use only)

**Q: OAuth callback not working in development?**
A: 
- Ensure callback endpoint exists and is reachable
- Use exact URL registered with ANAF (including port)
- Check firewall/antivirus isn't blocking connections

---

## 📞 Support & Contributing

### 🐛 Issue Reporting

Found a bug or need help? Please create an issue on GitHub:

**Before creating an issue:**
1. Search existing issues to avoid duplicates
2. Test with the latest version
3. Gather relevant information

**Include in your issue:**
- **Environment**: .NET version, OS, certificate provider
- **Configuration**: Sanitized configuration (remove secrets!)
- **Error details**: Full exception messages and stack traces
- **Steps to reproduce**: Minimal code example

**Example issue template:**
```markdown
## Environment
- RoEFactura version: 2.0.0
- .NET version: .NET 9.0
- OS: Windows 11
- Certificate: CERTSIGN

## Problem Description
Token exchange fails with 400 Bad Request

## Configuration
```json
{
  "AnafOAuth": {
    "ClientId": "test_client_id",
    "RedirectUri": "http://localhost:5000/callback"
  }
}
```

## Error Details
```
InvalidOperationException: Token exchange failed. HTTP Status: 400. 
Response: {"error":"invalid_client","error_description":"Client authentication failed"}
```

## Steps to Reproduce
1. Configure OAuth with above settings
2. Call InitiateOAuth()
3. Complete browser OAuth flow
4. Token exchange fails in callback
```

### 🤝 Contributing

We welcome contributions! Here's how to get started:

#### **Development Setup**
1. Fork the repository
2. Clone your fork: `git clone https://github.com/yourusername/RoEFactura.git`
3. Create feature branch: `git checkout -b feature/your-feature-name`
4. Install dependencies: `dotnet restore`

#### **Making Changes**
1. Follow existing code style and patterns
2. Add unit tests for new functionality
3. Update documentation if needed
4. Test your changes thoroughly

#### **Submitting Changes**
1. Push to your fork: `git push origin feature/your-feature-name`
2. Create Pull Request with:
   - Clear description of changes
   - Link to related issues
   - Screenshots/examples if applicable

#### **Code Style Guidelines**
- Use C# naming conventions (PascalCase for public members)
- Add XML documentation for public APIs
- Include comprehensive error handling
- Write descriptive commit messages

### 📄 License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

```
MIT License

Copyright (c) 2024 RoEFactura Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## 🎯 Quick Reference

### Installation
```bash
dotnet add package RoEFactura
```

### Basic Setup (Certificate)
```csharp
services.AddRoEFactura();
var token = await anafClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
```

### OAuth Setup (Web Apps)  
```csharp
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");
var authUrl = anafClient.GenerateAuthorizationUrl(options, state);
var token = await anafClient.ExchangeAuthorizationCodeAsync(code, options);
```

### Configuration
```json
{
  "AnafOAuth": {
    "ClientId": "your_client_id",
    "ClientSecret": "your_client_secret", 
    "RedirectUri": "https://yourapp.com/oauth/callback"
  }
}
```

### Common Operations
```csharp
// Get invoices
var invoices = await invoiceClient.ListEInvoicesAsync(token.AccessToken, 30, cui);

// Download invoice
var zipData = await invoiceClient.DownloadEInvoiceAsync(token.AccessToken, downloadId);

// Upload invoice
var result = await invoiceClient.UploadEInvoiceAsync(token.AccessToken, xmlContent);
```

---

**Made with ❤️ for the Romanian developer community**

*This library aims to simplify ANAF eInvoicing integration for all Romanian businesses and developers. If you find it useful, please consider giving it a ⭐ on GitHub!*