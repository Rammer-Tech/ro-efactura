import React, { useState, useEffect } from 'react';
import { anafService, AnafOAuthPopup } from '../services/anafService';

const AnafAuthTest = () => {
  const [certificates, setCertificates] = useState([]);
  const [selectedThumbprint, setSelectedThumbprint] = useState('');
  const [clientId, setClientId] = useState('test-client-id');
  const [clientSecret, setClientSecret] = useState('test-client-secret');
  const [callbackUrl, setCallbackUrl] = useState('');
  const [authResult, setAuthResult] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [activeTest, setActiveTest] = useState('');

  useEffect(() => {
    // Set default callback URL
    setCallbackUrl(`${window.location.origin}/anaf-callback.html`);
    loadCertificates();
  }, []);

  const loadCertificates = async () => {
    try {
      const result = await anafService.getAvailableCertificates();
      const certs = Array.isArray(result) ? result : result.data || [];
      setCertificates(certs);
      if (certs.length > 0) {
        setSelectedThumbprint(certs[0].thumbprint);
      }
    } catch (err) {
      console.error('Error loading certificates:', err);
    }
  };

  const testBackendAuthentication = async () => {
    setActiveTest('backend');
    setLoading(true);
    setError('');
    setAuthResult(null);
    
    try {
      const result = await anafService.testAnafAuthentication(clientId, clientSecret, callbackUrl);
      setAuthResult(result);
    } catch (err) {
      setError(`Backend authentication test failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const testBackendAuthenticationWithCert = async () => {
    if (!selectedThumbprint) {
      setError('Please select a certificate first');
      return;
    }

    setActiveTest('backend-cert');
    setLoading(true);
    setError('');
    setAuthResult(null);
    
    try {
      const result = await anafService.testAnafAuthenticationWithCert(
        selectedThumbprint, 
        clientId, 
        clientSecret, 
        callbackUrl
      );
      setAuthResult(result);
    } catch (err) {
      setError(`Backend authentication with certificate failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const testBrowserRedirect = async () => {
    setActiveTest('redirect');
    setLoading(true);
    setError('');
    
    try {
      const result = await anafService.generateAuthUrl(clientId, callbackUrl);
      if (result.success && result.data?.authUrl) {
        // Redirect the entire window
        window.location.href = result.data.authUrl;
      } else {
        setError('Failed to generate auth URL');
        setLoading(false);
      }
    } catch (err) {
      setError(`Browser redirect test failed: ${err.message}`);
      setLoading(false);
    }
  };

  const testPopupAuthentication = async () => {
    setActiveTest('popup');
    setLoading(true);
    setError('');
    setAuthResult(null);
    
    try {
      const result = await anafService.generateAuthUrl(clientId, callbackUrl);
      if (!result.success || !result.data?.authUrl) {
        setError('Failed to generate auth URL');
        setLoading(false);
        return;
      }

      const popup = new AnafOAuthPopup(
        result.data.authUrl,
        async (code) => {
          console.log('Received authorization code:', code);
          try {
            // Exchange code for token
            const tokenResult = await anafService.mockTokenExchange(
              code, 
              clientId, 
              clientSecret, 
              callbackUrl
            );
            setAuthResult({
              success: true,
              message: 'Popup authentication successful!',
              data: { code, token: tokenResult.data }
            });
          } catch (err) {
            setError(`Token exchange failed: ${err.message}`);
          } finally {
            setLoading(false);
          }
        },
        (error) => {
          setError(`Popup authentication failed: ${error}`);
          setLoading(false);
        }
      );

      popup.start();
    } catch (err) {
      setError(`Popup authentication test failed: ${err.message}`);
      setLoading(false);
    }
  };

  const testRealAnafUrl = () => {
    const realAnafUrl = 'https://logincert.anaf.ro/anaf-oauth2/v1/authorize?' +
      `response_type=code&` +
      `client_id=${encodeURIComponent(clientId)}&` +
      `redirect_uri=${encodeURIComponent(callbackUrl)}&` +
      `token_content_type=jwt`;
    
    const confirmed = window.confirm(
      'This will open the real ANAF authentication page. ' +
      'Make sure you have valid client credentials and certificates. Continue?'
    );
    
    if (confirmed) {
      window.open(realAnafUrl, '_blank');
    }
  };

  return (
    <div className="container mt-4">
      <h2>🔐 ANAF Authentication Testing</h2>
      
      {/* Configuration */}
      <div className="card mb-4">
        <div className="card-header">
          <h5>Configuration</h5>
        </div>
        <div className="card-body">
          <div className="row">
            <div className="col-md-6">
              <div className="mb-3">
                <label className="form-label">Client ID</label>
                <input 
                  type="text" 
                  className="form-control" 
                  value={clientId}
                  onChange={(e) => setClientId(e.target.value)}
                  placeholder="Your ANAF client ID"
                />
              </div>
              <div className="mb-3">
                <label className="form-label">Client Secret</label>
                <input 
                  type="password" 
                  className="form-control" 
                  value={clientSecret}
                  onChange={(e) => setClientSecret(e.target.value)}
                  placeholder="Your ANAF client secret"
                />
              </div>
            </div>
            <div className="col-md-6">
              <div className="mb-3">
                <label className="form-label">Callback URL</label>
                <input 
                  type="url" 
                  className="form-control" 
                  value={callbackUrl}
                  onChange={(e) => setCallbackUrl(e.target.value)}
                  placeholder="OAuth callback URL"
                />
              </div>
              <div className="mb-3">
                <label className="form-label">Certificate</label>
                <select 
                  className="form-select"
                  value={selectedThumbprint}
                  onChange={(e) => setSelectedThumbprint(e.target.value)}
                >
                  <option value="">Select certificate...</option>
                  {certificates.map((cert) => (
                    <option key={cert.thumbprint} value={cert.thumbprint}>
                      {cert.subject?.split(',')[0]?.replace('CN=', '') || 'Unknown'} 
                      ({cert.statusDescription || 'Status Unknown'})
                    </option>
                  ))}
                </select>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Test Buttons */}
      <div className="card mb-4">
        <div className="card-header">
          <h5>Authentication Tests</h5>
        </div>
        <div className="card-body">
          {/* Authentication Method Explanations */}
          <div className="row mb-4">
            <div className="col-12">
              <h6 className="text-muted mb-3">🔍 Understanding Each Authentication Method</h6>
              
              <div className="row g-3">
                <div className="col-md-6">
                  <div className="card border-primary">
                    <div className="card-header bg-primary text-white">
                      <h6 className="mb-0">🔧 Backend Certificate Auth</h6>
                    </div>
                    <div className="card-body p-3">
                      <p className="small mb-2">
                        <strong>How it works:</strong> Server automatically finds and uses Romanian certificates 
                        installed in Windows Certificate Store. The .NET backend performs mTLS authentication 
                        with ANAF using the certificate.
                      </p>
                      <p className="small mb-0 text-muted">
                        <strong>Use case:</strong> Desktop apps, server-to-server authentication, or web apps 
                        where the server has access to certificates.
                      </p>
                    </div>
                  </div>
                </div>
                
                <div className="col-md-6">
                  <div className="card border-info">
                    <div className="card-header bg-info text-white">
                      <h6 className="mb-0">🔧 Specific Certificate Auth</h6>
                    </div>
                    <div className="card-body p-3">
                      <p className="small mb-2">
                        <strong>How it works:</strong> Same as above but uses a specific certificate 
                        you select from the dropdown. Demonstrates how to target a particular certificate 
                        by thumbprint.
                      </p>
                      <p className="small mb-0 text-muted">
                        <strong>Use case:</strong> When you need to use a specific certificate (e.g., 
                        company cert vs personal cert).
                      </p>
                    </div>
                  </div>
                </div>
                
                <div className="col-md-6">
                  <div className="card border-warning">
                    <div className="card-header bg-warning text-dark">
                      <h6 className="mb-0">🌐 Browser Full Redirect</h6>
                    </div>
                    <div className="card-body p-3">
                      <p className="small mb-2">
                        <strong>How it works:</strong> Redirects your entire browser to ANAF OAuth page. 
                        Browser will show certificate selection dialog, you choose your certificate, 
                        enter PIN, then get redirected back.
                      </p>
                      <p className="small mb-0 text-muted">
                        <strong>Use case:</strong> Traditional OAuth flow. This is how SmartBill, KEez, 
                        and similar web apps work.
                      </p>
                    </div>
                  </div>
                </div>
                
                <div className="col-md-6">
                  <div className="card border-success">
                    <div className="card-header bg-success text-white">
                      <h6 className="mb-0">📱 Popup OAuth Flow</h6>
                    </div>
                    <div className="card-body p-3">
                      <p className="small mb-2">
                        <strong>How it works:</strong> Opens ANAF OAuth in a popup window. Browser shows 
                        certificate selection, you authenticate, popup closes and sends result back to 
                        main window via PostMessage API.
                      </p>
                      <p className="small mb-0 text-muted">
                        <strong>Use case:</strong> Better UX - user stays in your app. More complex but 
                        cleaner integration.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
          
          {/* Test Buttons */}
          <div className="d-grid gap-2 d-md-flex">
            <button 
              className="btn btn-primary" 
              onClick={testBackendAuthentication}
              disabled={loading}
              data-bs-toggle="tooltip" 
              title="Tests server-side certificate authentication with auto-discovery"
            >
              {activeTest === 'backend' && loading ? 'Testing...' : '🔧 Backend Auth (Auto)'}
            </button>
            
            <button 
              className="btn btn-info" 
              onClick={testBackendAuthenticationWithCert}
              disabled={loading || !selectedThumbprint}
              data-bs-toggle="tooltip" 
              title="Tests server-side authentication with your selected certificate"
            >
              {activeTest === 'backend-cert' && loading ? 'Testing...' : '🔧 Backend Auth (Selected)'}
            </button>
            
            <button 
              className="btn btn-warning" 
              onClick={testBrowserRedirect}
              disabled={loading}
              data-bs-toggle="tooltip" 
              title="Redirects browser to ANAF for certificate selection"
            >
              {activeTest === 'redirect' && loading ? 'Redirecting...' : '🌐 Browser Redirect'}
            </button>
            
            <button 
              className="btn btn-success" 
              onClick={testPopupAuthentication}
              disabled={loading}
              data-bs-toggle="tooltip" 
              title="Opens ANAF authentication in popup window"
            >
              {activeTest === 'popup' && loading ? 'Testing...' : '📱 Popup OAuth'}
            </button>
            
            <button 
              className="btn btn-danger" 
              onClick={testRealAnafUrl}
              disabled={loading}
              data-bs-toggle="tooltip" 
              title="Opens real ANAF URL in new tab (requires valid credentials)"
            >
              🚀 Real ANAF URL
            </button>
          </div>
          
          <div className="mt-4 p-3 bg-light rounded">
            <h6 className="text-primary mb-2">💡 Key Insights for Web Development</h6>
            <ul className="small mb-0">
              <li><strong>Certificate Access:</strong> Browsers can access certificates through mTLS during HTTPS requests, not via JavaScript</li>
              <li><strong>Authentication Flow:</strong> OAuth redirect → Certificate selection → PIN entry → Authorization code → Token exchange</li>
              <li><strong>User Experience:</strong> Popup method provides better UX but requires PostMessage communication</li>
              <li><strong>Certificate Selection:</strong> Browser handles certificate UI automatically during mTLS handshake</li>
              <li><strong>Real Implementation:</strong> Production apps combine server-side token handling with browser-based OAuth flows</li>
            </ul>
          </div>
        </div>
      </div>

      {/* Error Display */}
      {error && (
        <div className="alert alert-danger">
          <h5>Error</h5>
          <p>{error}</p>
          <button 
            className="btn btn-outline-danger btn-sm" 
            onClick={() => setError('')}
          >
            Dismiss
          </button>
        </div>
      )}

      {/* Results Display */}
      {authResult && (
        <div className={`alert ${authResult.success ? 'alert-success' : 'alert-danger'}`}>
          <h5>{authResult.success ? '✅ Success' : '❌ Failed'}</h5>
          <p>{authResult.message}</p>
          {authResult.data && (
            <details>
              <summary>Response Data</summary>
              <pre className="mt-2" style={{ fontSize: '0.8em', maxHeight: '300px', overflow: 'auto' }}>
                {JSON.stringify(authResult.data, null, 2)}
              </pre>
            </details>
          )}
          {authResult.error && (
            <div className="mt-2">
              <strong>Error Details:</strong> {authResult.error}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

export default AnafAuthTest;