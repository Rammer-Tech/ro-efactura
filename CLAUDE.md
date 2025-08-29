# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

RoEFactura is a .NET 9.0 library for integrating with the Romanian ANAF (National Agency for Fiscal Administration) e-invoicing system. It provides comprehensive eFactura integration including dual authentication methods (certificate-based and OAuth web flows), complete UBL 2.1 document processing with Romanian RO_CIUS validation, and advanced invoice processing capabilities through ANAF's REST API endpoints.

## Build and Development Commands

```bash
# Build the project
dotnet build

# Build in Release mode
dotnet build -c Release

# Restore NuGet packages
dotnet restore

# Clean build artifacts
dotnet clean

# Create NuGet package
dotnet pack
```

## Architecture

### Core Components

1. **Authentication Layer** (`Services/Authentication/`)
   - `AnafOAuthClient`: Dual authentication support
     - **Certificate-based**: Traditional desktop app authentication using Romanian digital certificates
     - **OAuth web flow**: Modern web application integration following SmartBill's exact pattern
   - `Token`: JWT token model for API authentication
   - `CertificateInfo`: Certificate metadata and validation
   - Certificate validation supports multiple Romanian certificate providers (CERTSIGN, DIGISIGN, ALFASIGN, etc.)
   - State management for CSRF protection in OAuth flows

2. **API Client** (`Services/Api/`)
   - `AnafEInvoiceClient`: Main client for e-invoice operations
     - List invoices (paged and non-paged)
     - Download invoices as ZIP files
     - Validate XML invoices
     - Upload XML invoices
     - Process downloaded invoices with UBL validation
     - Batch processing capabilities
   - Supports both production and test environments (currently hardcoded to production)

3. **UBL Processing Engine** (`Services/Processing/`)
   - `UblProcessingService`: Complete UBL 2.1 document processing pipeline
     - XML parsing and deserialization
     - ZIP archive extraction and processing
     - Romanian RO_CIUS validation
     - Processing statistics and monitoring
     - Batch processing support

4. **Validation Engine** (`Validation/`)
   - `RoCiusUblValidator`: Complete EN 16931 + RO_CIUS compliance validation
   - Romanian-specific validators for parties, addresses, invoice lines
   - `RomanianConstants`: Complete validation sets (county codes, invoice types, VAT codes)
   - Comprehensive error reporting and validation results

5. **Extensions** (`Extensions/`)
   - `InvoiceTypeExtensions`: Rich invoice analysis capabilities
     - Romanian invoice detection
     - Currency and totals extraction
     - Validation summaries
   - `PartyExtensions`: Party information processing
   - `UblSharpExtensions`: UBL document manipulation utilities

6. **Models and DTOs** (`Models/`, `Dtos/`)
   - `AnafOAuthOptions`: Configurable OAuth settings
   - `OAuthModels`: Complete OAuth flow models (initiation, token exchange, status)
   - `ProcessingResult<T>`: Generic result wrapper with validation errors
   - ANAF API response models for all endpoints

7. **Dependency Injection** (`ServiceCollectionExtensions.cs`)
   - **Three registration patterns**:
     - `AddRoEFactura()`: Basic services registration
     - `AddRoEFacturaWithOAuth(options)`: OAuth with direct configuration
     - `AddRoEFacturaWithOAuth(configuration)`: OAuth with appsettings.json binding
   - Uses `HttpClient` factory pattern for proper HTTP client management
   - FluentValidation integration for all validators

8. **Utilities** (`Utilities/`)
   - `XmlFileDeserializer`: Generic XML deserialization from files

### Key Dependencies

- **Ardalis.GuardClauses**: Input validation and defensive programming
- **Microsoft.Extensions.Hosting.Abstractions**: Environment detection
- **Microsoft.Extensions.Http**: HTTP client factory and lifecycle management
- **Newtonsoft.Json**: JSON serialization for ANAF API responses
- **UblSharp**: UBL 2.1 document parsing and manipulation
- **UblSharp.Validation**: UBL document validation capabilities
- **FluentValidation**: Comprehensive validation framework
- **FluentValidation.DependencyInjectionExtensions**: DI integration for validators

### ANAF API Endpoints

**Production endpoints:**
- List invoices (paged): `https://api.anaf.ro/prod/FCTEL/rest/listaMesajePaginatieFactura`
- List invoices (non-paged): `https://api.anaf.ro/prod/FCTEL/rest/listaMesajeFactura`
- Download invoice: `https://api.anaf.ro/prod/FCTEL/rest/descarcare`
- Validate XML: `https://api.anaf.ro/prod/efactura/validare`
- Upload XML: `https://api.anaf.ro/prod/efactura/upload`

**OAuth endpoints:**
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
4. Code exchanged for JWT access token using Basic authentication
5. Bearer token used for subsequent API calls
6. Follows SmartBill's exact OAuth pattern with `token_content_type=jwt`

### Romanian RO_CIUS Validation

**Key validation rules implemented:**
- **BR-RO-010**: Invoice number must contain at least one digit
- **BR-RO-020**: Invoice type code must be one of: 380, 389, 384, 381, 751
- **BR-RO-120**: Romanian buyers must have fiscal identification code (CUI/CIF)
- **BR-RO-130**: Enforcement seizure scenarios require specific payee information
- **Length limits**: All Romanian-specific field length restrictions
- **County codes**: ISO 3166-2:RO validation for Romanian addresses
- **Currency rules**: If currency ≠ RON, VAT currency must be RON
- **Multiplicity limits**: Maximum occurrences for various business groups (e.g., max 999 invoice lines)

**Validation features:**
- Complete EN 16931 compliance checking
- Romanian address validation (including Bucharest sectors)
- VAT category and rate validation
- Monetary totals consistency checks
- Invoice line validation with UBL mapping

### Usage Patterns

#### **Basic Setup (Certificate-based)**
```csharp
// In Startup/Program.cs
services.AddRoEFactura();

// Usage - Certificate authentication
var token = await anafOAuthClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
var invoices = await anafEInvoiceClient.ListEInvoicesAsync(token.AccessToken, days, cui);
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

// Or from configuration
services.AddRoEFacturaWithOAuth(configuration, "AnafOAuth");

// Usage - OAuth web flow
var authUrl = anafOAuthClient.GenerateAuthorizationUrl(options, state);
// Redirect user to authUrl
// On callback:
var token = await anafOAuthClient.ExchangeAuthorizationCodeAsync(code, options);
var invoices = await anafEInvoiceClient.ListEInvoicesAsync(token.AccessToken, days, cui);
```

#### **UBL Processing**
```csharp
// Process downloaded invoice with validation
var result = await anafEInvoiceClient.ProcessDownloadedInvoiceAsync(token.AccessToken, downloadId);
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
        Console.WriteLine($"{error.PropertyName}: {error.ErrorMessage}");
    }
}

// Batch processing
var downloadIds = new[] { "id1", "id2", "id3" };
var results = await anafEInvoiceClient.ProcessMultipleInvoicesAsync(token.AccessToken, downloadIds);
```

#### **Custom Validation**
```csharp
// Validate XML content
var xmlContent = File.ReadAllText("invoice.xml");
var validationResult = await anafEInvoiceClient.ValidateInvoiceXmlAsync(xmlContent);
if (!validationResult.IsSuccess)
{
    // Handle Romanian RO_CIUS validation errors
    var errors = validationResult.Errors.Where(e => e.ErrorCode.StartsWith("BR-RO-"));
}
```

## Important Notes

### **Authentication**
- Certificate selection is automatic based on Romanian CA issuer name patterns
- OAuth web flow includes CSRF protection via state parameters
- Token validation and expiry handling built-in
- Support for both hardcoded and configuration-based OAuth setup

### **Validation & Processing**
- Complete EN 16931 + RO_CIUS compliance validation
- All Romanian-specific business rules (BR-RO-*) implemented
- UBL 2.1 document processing with ZIP archive support
- Processing statistics and monitoring capabilities
- Batch processing for multiple invoices

### **Error Handling**
- All API methods include Guard clauses for input validation
- Comprehensive validation results with specific error codes
- Romanian validation rules with detailed error messages
- File operations create directories if they don't exist
- Descriptive exceptions with error context

### **Development**
- No test project currently exists in the solution
- FluentValidation integration for extensible validation rules
- Logging integration throughout the processing pipeline
- Thread-safe processing statistics tracking

### **Production Considerations**
- Currently hardcoded to production ANAF endpoints
- HTTP client factory pattern for proper connection management
- Romanian address validation including Bucharest sector handling
- Currency conversion rules for non-RON invoices
- Maximum limits enforced (999 invoice lines, 500 preceding invoices, etc.)