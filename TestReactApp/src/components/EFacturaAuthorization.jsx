import React, { useState, useEffect } from 'react';
import { eFacturaService } from '../services/eFacturaService';

const EFacturaAuthorization = () => {
  const [authStatus, setAuthStatus] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [debugInfo, setDebugInfo] = useState(null);
  const [showDebug, setShowDebug] = useState(false);

  useEffect(() => {
    // Check for OAuth callback parameters
    const params = new URLSearchParams(window.location.search);
    if (params.get('success') === 'true') {
      // Success callback from OAuth
      showSuccessMessage();
      // Clean URL
      window.history.replaceState({}, document.title, '/efactura-auth');
    } else if (params.get('error')) {
      // Error callback from OAuth
      setError(`Authorization failed: ${params.get('error')}`);
      // Clean URL
      window.history.replaceState({}, document.title, '/efactura-auth');
    }
    
    // Load initial status
    checkAuthStatus();
    
    // Set up auto-refresh every 30 seconds
    const interval = setInterval(checkAuthStatus, 30000);
    return () => clearInterval(interval);
  }, []);

  const checkAuthStatus = async () => {
    try {
      const status = await eFacturaService.getAuthStatus();
      setAuthStatus(status);
      setDebugInfo(status.tokenInfo);
    } catch (err) {
      console.error('Error checking auth status:', err);
    }
  };

  const showSuccessMessage = () => {
    setError('');
    const successMsg = document.createElement('div');
    successMsg.className = 'alert alert-success alert-dismissible fade show position-fixed top-0 start-50 translate-middle-x mt-3';
    successMsg.style.zIndex = '9999';
    successMsg.innerHTML = `
      <strong>✅ Authorization Successful!</strong> You are now connected to ANAF eFactura.
      <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    document.body.appendChild(successMsg);
    setTimeout(() => successMsg.remove(), 5000);
    checkAuthStatus();
  };

  const handleAuthorize = async () => {
    setLoading(true);
    setError('');
    
    try {
      const result = await eFacturaService.initiateOAuth();
      
      if (result.success && result.authUrl) {
        // Redirect to ANAF OAuth page (exactly like SmartBill)
        window.location.href = result.authUrl;
      } else {
        setError(result.error || 'Failed to initiate authorization');
      }
    } catch (err) {
      setError(`Authorization failed: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const handleClearAuth = async () => {
    if (!window.confirm('Are you sure you want to disconnect from ANAF eFactura?')) {
      return;
    }
    
    setLoading(true);
    try {
      await eFacturaService.clearAuthorization();
      await checkAuthStatus();
      setError('');
    } catch (err) {
      setError(`Failed to clear authorization: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const formatExpiresIn = (expiresAt) => {
    if (!expiresAt) return 'N/A';
    
    const now = new Date();
    const expires = new Date(expiresAt);
    const diffMs = expires - now;
    
    if (diffMs <= 0) return 'Expired';
    
    const hours = Math.floor(diffMs / (1000 * 60 * 60));
    const minutes = Math.floor((diffMs % (1000 * 60 * 60)) / (1000 * 60));
    
    if (hours > 0) {
      return `${hours}h ${minutes}m`;
    }
    return `${minutes} minutes`;
  };

  return (
    <div className="container mt-4">
      {/* Main Authorization Card */}
      <div className="card mb-4">
        <div className="card-header bg-primary text-white">
          <h5 className="mb-0">🔐 ANAF eFactura Authorization</h5>
        </div>
        <div className="card-body">
          {/* Status Indicator */}
          {authStatus && (
            <div className={`alert ${authStatus.isAuthorized ? 'alert-success' : 'alert-warning'} mb-4`}>
              <div className="d-flex justify-content-between align-items-center">
                <div>
                  {authStatus.isAuthorized ? (
                    <>
                      <strong>✅ Connected to ANAF eFactura</strong>
                      <div className="small mt-1">
                        Token expires in: <strong>{formatExpiresIn(authStatus.expiresAt)}</strong>
                      </div>
                    </>
                  ) : (
                    <strong>⚠️ Not connected - Click below to authorize</strong>
                  )}
                </div>
                {authStatus.isAuthorized && (
                  <button 
                    className="btn btn-sm btn-outline-danger" 
                    onClick={handleClearAuth}
                    disabled={loading}
                  >
                    Disconnect
                  </button>
                )}
              </div>
            </div>
          )}

          {/* Information Panel */}
          <div className="row mb-4">
            <div className="col-md-12">
              <div className="card bg-light">
                <div className="card-body">
                  <h6 className="card-title text-primary">📋 What is eFactura Integration?</h6>
                  <p className="card-text small">
                    This integration connects your application with the Romanian National Centralized eFactura System (SPV).
                  </p>
                  <div className="row mt-3">
                    <div className="col-md-6">
                      <h6 className="text-secondary small">Features:</h6>
                      <ul className="small">
                        <li>Automatic import of income invoices</li>
                        <li>Automatic import of expense invoices</li>
                        <li>Real-time synchronization with SPV</li>
                        <li>Secure OAuth 2.0 authentication</li>
                      </ul>
                    </div>
                    <div className="col-md-6">
                      <h6 className="text-secondary small">Benefits:</h6>
                      <ul className="small">
                        <li>Eliminate manual data entry</li>
                        <li>Reduce accounting errors</li>
                        <li>Stay compliant with regulations</li>
                        <li>Save time on invoice management</li>
                      </ul>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Authorization Process Info */}
          <div className="alert alert-info mb-4">
            <h6 className="alert-heading">🔍 Authorization Process</h6>
            <ol className="small mb-0">
              <li>Click "Connect to eFactura" below</li>
              <li>You'll be redirected to ANAF's secure login page</li>
              <li>Select your digital certificate and enter your PIN</li>
              <li>Approve the authorization request</li>
              <li>You'll be redirected back here with access granted</li>
            </ol>
          </div>

          {/* Action Buttons */}
          <div className="d-grid gap-2 d-md-block">
            {!authStatus?.isAuthorized ? (
              <button 
                className="btn btn-primary btn-lg" 
                onClick={handleAuthorize}
                disabled={loading}
              >
                {loading ? (
                  <>
                    <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                    Connecting...
                  </>
                ) : (
                  <>🔗 Connect to eFactura</>
                )}
              </button>
            ) : (
              <button 
                className="btn btn-success btn-lg" 
                disabled
              >
                ✅ Connected to eFactura
              </button>
            )}
            
            <button 
              className="btn btn-outline-secondary ms-md-2" 
              onClick={() => setShowDebug(!showDebug)}
            >
              {showDebug ? '🔒 Hide' : '🔍 Show'} Debug Info
            </button>
          </div>

          {/* Error Display */}
          {error && (
            <div className="alert alert-danger mt-3">
              <strong>Error:</strong> {error}
              <button 
                type="button" 
                className="btn-close float-end" 
                onClick={() => setError('')}
              ></button>
            </div>
          )}
        </div>
      </div>

      {/* Debug Information Panel */}
      {showDebug && (
        <div className="card">
          <div className="card-header bg-dark text-white">
            <h6 className="mb-0">🐛 Debug Information</h6>
          </div>
          <div className="card-body">
            <div className="row">
              <div className="col-md-6">
                <h6 className="text-muted">Authorization Status</h6>
                <pre className="bg-light p-2 rounded" style={{ fontSize: '0.85em' }}>
                  {JSON.stringify(authStatus, null, 2)}
                </pre>
              </div>
              <div className="col-md-6">
                <h6 className="text-muted">Token Information</h6>
                <pre className="bg-light p-2 rounded" style={{ fontSize: '0.85em' }}>
                  {JSON.stringify(debugInfo, null, 2)}
                </pre>
              </div>
            </div>
            
            <div className="mt-3">
              <h6 className="text-muted">OAuth Configuration</h6>
              <table className="table table-sm">
                <tbody>
                  <tr>
                    <td><strong>Authorize URL:</strong></td>
                    <td><code>https://logincert.anaf.ro/anaf-oauth2/v1/authorize</code></td>
                  </tr>
                  <tr>
                    <td><strong>Token URL:</strong></td>
                    <td><code>https://logincert.anaf.ro/anaf-oauth2/v1/token</code></td>
                  </tr>
                  <tr>
                    <td><strong>Redirect URI:</strong></td>
                    <td><code>http://localhost:5126/api/efactura/oauth/callback</code></td>
                  </tr>
                  <tr>
                    <td><strong>Response Type:</strong></td>
                    <td><code>code</code></td>
                  </tr>
                  <tr>
                    <td><strong>Token Content Type:</strong></td>
                    <td><code>jwt</code></td>
                  </tr>
                </tbody>
              </table>
            </div>
            
            <div className="mt-3">
              <button 
                className="btn btn-sm btn-warning" 
                onClick={checkAuthStatus}
              >
                🔄 Refresh Status
              </button>
              {authStatus?.isAuthorized && (
                <button 
                  className="btn btn-sm btn-danger ms-2" 
                  onClick={handleClearAuth}
                >
                  🗑️ Clear Authorization
                </button>
              )}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

export default EFacturaAuthorization;