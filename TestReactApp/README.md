# Romanian Certificate Authentication Test App

This is a test React application for experimenting with Romanian digital certificate authentication patterns.

## Setup

1. **Install dependencies:**
   ```bash
   npm install
   ```

2. **Start the backend (TestCertificate):**
   ```bash
   cd ../TestCertificate
   dotnet run
   ```
   Backend will run on: `https://localhost:7205`

3. **Start the React app:**
   ```bash
   npm start
   ```
   Frontend will run on: `http://localhost:3000`

## Features

### Certificate Testing
- **Certificate Detection**: Automatically finds Romanian certificates in Windows Certificate Store
- **Certificate Validation**: Checks if certificates are valid for client authentication
- **Certificate Details**: Shows certificate information (subject, issuer, expiration, etc.)
- **Cache Management**: Clear certificate cache for testing

### Authentication Testing
- **Backend Authentication**: Test server-side certificate authentication
- **Browser Redirect**: Test full-page redirect to ANAF OAuth
- **Popup Authentication**: Test popup-based OAuth flow
- **Real ANAF Integration**: Test with actual ANAF endpoints

## Testing Patterns

### 1. Backend Certificate Authentication
- Uses server-side certificate from Windows Certificate Store
- Good for understanding how certificates work
- Tests the RoEFactura library integration

### 2. Browser Redirect Flow
- Redirects entire browser window to ANAF OAuth
- Browser will show certificate selection dialog
- Returns to callback page with authorization code

### 3. Popup Authentication Flow  
- Opens ANAF OAuth in popup window
- Uses PostMessage for communication
- Better user experience (stays in main app)

## Endpoints Tested

### Certificate Endpoints
- `GET /api/test/certificates` - List available certificates
- `GET /api/test/certificates/{thumbprint}` - Get certificate details
- `POST /api/test/certificates/clear-cache` - Clear certificate cache
- `GET /api/test/certificates/health` - Certificate system health

### ANAF Authentication Endpoints
- `POST /api/test/anaf/authenticate` - Test auth with auto-discovered cert
- `POST /api/test/anaf/authenticate/{thumbprint}` - Test auth with specific cert
- `POST /api/test/anaf/token-exchange` - Mock token exchange
- `POST /api/test/anaf/auth-url` - Generate OAuth URL
- `GET /api/test/anaf/health` - ANAF endpoints health

## Expected Certificate Authorities

The system looks for certificates from these Romanian CAs:
- CERTSIGN
- DIGISIGN  
- ALFASIGN
- CERTDIGITAL
- TRANSIGNE
- Other Romanian certificate providers

## Notes

- This is a **test environment** - not for production use
- Requires Romanian digital certificates installed in Windows Certificate Store
- Backend must be running with HTTPS for certificate authentication
- CORS is configured to allow React app communication

## Troubleshooting

1. **No certificates found**: Make sure Romanian digital certificates are installed in Windows
2. **CORS errors**: Ensure backend is running on `https://localhost:7205` 
3. **Certificate selection not showing**: Try the popup authentication method
4. **SSL errors**: Accept the self-signed certificate in browser for localhost