# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

RoEFactura is a .NET 8.0 library for integrating with the Romanian ANAF (National Agency for Fiscal Administration) e-invoicing system. It provides OAuth authentication and API client services for managing electronic invoices (e-factura) through ANAF's REST API endpoints.

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
   - `AnafOAuthClient`: Handles OAuth2 authentication with ANAF using client certificates
   - `Token`: JWT token model for API authentication
   - Certificate validation supports multiple Romanian certificate providers (CERTSIGN, DIGISIGN, ALFASIGN, etc.)

2. **API Client** (`Services/Api/`)
   - `AnafEInvoiceClient`: Main client for e-invoice operations
     - List invoices (paged and non-paged)
     - Download invoices as ZIP files
     - Validate XML invoices
     - Upload XML invoices
   - Supports both production and test environments (currently hardcoded to production)

3. **Dependency Injection** (`ServiceCollectionExtensions.cs`)
   - Extension method `AddRoEFactura()` registers all required services
   - Uses `HttpClient` factory pattern for proper HTTP client management

4. **Utilities** (`Utilities/`)
   - `XmlFileDeserializer`: Generic XML deserialization from files

### Key Dependencies

- **Ardalis.GuardClauses**: Input validation
- **Microsoft.Extensions.Hosting.Abstractions**: Environment detection
- **Microsoft.Extensions.Http**: HTTP client factory
- **Newtonsoft.Json**: JSON serialization

### ANAF API Endpoints

Production endpoints:
- List invoices (paged): `https://api.anaf.ro/prod/FCTEL/rest/listaMesajePaginatieFactura`
- List invoices (non-paged): `https://api.anaf.ro/prod/FCTEL/rest/listaMesajeFactura`
- Download invoice: `https://api.anaf.ro/prod/FCTEL/rest/descarcare`
- Validate XML: `https://api.anaf.ro/prod/efactura/validare`
- Upload XML: `https://api.anaf.ro/prod/efactura/upload`

OAuth endpoints:
- Authorize: `https://logincert.anaf.ro/anaf-oauth2/v1/authorize`
- Token: `https://logincert.anaf.ro/anaf-oauth2/v1/token`

### Authentication Flow

1. Client certificate is loaded from Windows Certificate Store (CurrentUser/My)
2. OAuth2 authorization code flow with JWT tokens
3. Bearer token authentication for API calls

### Usage Pattern

```csharp
// In Startup/Program.cs
services.AddRoEFactura();

// Usage
var token = await anafOAuthClient.GetAccessTokenAsync(clientId, clientSecret, callbackUrl);
var invoices = await anafEInvoiceClient.ListEInvoicesAsync(token.AccessToken, days, cui);
```

## Important Notes

- Certificate selection is automatic based on issuer name patterns
- All API methods include Guard clauses for input validation
- File operations create directories if they don't exist
- Error handling throws exceptions with descriptive messages
- No test project currently exists in the solution