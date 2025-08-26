import React, { useState, useEffect } from 'react';
import { anafService } from '../services/anafService';

const CertificateTest = () => {
  const [certificates, setCertificates] = useState([]);
  const [selectedCert, setSelectedCert] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [healthStatus, setHealthStatus] = useState(null);

  useEffect(() => {
    loadCertificates();
    checkHealth();
  }, []);

  const loadCertificates = async () => {
    setLoading(true);
    setError('');
    try {
      const result = await anafService.getAvailableCertificates();
      if (Array.isArray(result)) {
        setCertificates(result);
      } else if (result.success) {
        setCertificates(result.data || []);
      } else {
        setError(result.message || 'Failed to load certificates');
      }
    } catch (err) {
      setError(`Error loading certificates: ${err.message}`);
      console.error('Certificate loading error:', err);
    } finally {
      setLoading(false);
    }
  };

  const checkHealth = async () => {
    try {
      const result = await anafService.certificateHealthCheck();
      setHealthStatus(result);
    } catch (err) {
      console.error('Health check error:', err);
    }
  };

  const clearCache = async () => {
    setLoading(true);
    try {
      const result = await anafService.clearCertificateCache();
      if (result.success) {
        alert('Certificate cache cleared successfully!');
        await loadCertificates();
      } else {
        setError(result.message || 'Failed to clear cache');
      }
    } catch (err) {
      setError(`Error clearing cache: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  const getCertificateInfo = async (thumbprint) => {
    setLoading(true);
    try {
      const result = await anafService.getCertificateInfo(thumbprint);
      if (result.success !== false) {
        setSelectedCert(result);
      } else {
        setError(result.message || 'Failed to get certificate info');
      }
    } catch (err) {
      setError(`Error getting certificate info: ${err.message}`);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="container mt-4">
      <div className="row">
        <div className="col-12">
          <h2>🔐 Certificate Detection & Testing</h2>
          
          {/* Health Status */}
          {healthStatus && (
            <div className={`alert ${healthStatus.success ? 'alert-success' : 'alert-warning'}`}>
              <h5>System Health</h5>
              <p>{healthStatus.message}</p>
              {healthStatus.data && (
                <small>
                  Total Certificates: {healthStatus.data.totalCerts}, 
                  Valid: {healthStatus.data.validCerts}
                </small>
              )}
            </div>
          )}

          {/* Controls */}
          <div className="mb-3">
            <button 
              className="btn btn-primary me-2" 
              onClick={loadCertificates} 
              disabled={loading}
            >
              {loading ? 'Loading...' : '🔄 Refresh Certificates'}
            </button>
            <button 
              className="btn btn-warning me-2" 
              onClick={clearCache}
              disabled={loading}
            >
              🗑️ Clear Cache
            </button>
          </div>

          {/* Error Display */}
          {error && (
            <div className="alert alert-danger">
              <strong>Error:</strong> {error}
              <button 
                type="button" 
                className="btn-close float-end" 
                onClick={() => setError('')}
              ></button>
            </div>
          )}

          {/* Certificates List */}
          <div className="row">
            <div className="col-md-6">
              <h4>Available Certificates ({certificates.length})</h4>
              {certificates.length === 0 ? (
                <div className="alert alert-info">
                  No certificates found. Make sure you have Romanian digital certificates installed.
                </div>
              ) : (
                <div className="list-group">
                  {certificates.map((cert, index) => (
                    <div 
                      key={cert.thumbprint || index} 
                      className={`list-group-item list-group-item-action ${
                        selectedCert?.thumbprint === cert.thumbprint ? 'active' : ''
                      }`}
                      onClick={() => getCertificateInfo(cert.thumbprint)}
                      style={{ cursor: 'pointer' }}
                    >
                      <div className="d-flex w-100 justify-content-between">
                        <h6 className="mb-1">
                          {cert.subject?.split(',')[0]?.replace('CN=', '') || 'Unknown'}
                        </h6>
                        <small className={`badge ${
                          cert.isValidForClientAuth ? 'bg-success' : 'bg-warning'
                        }`}>
                          {cert.statusDescription || (cert.isValidForClientAuth ? '✅ Valid' : '⚠️ Invalid')}
                        </small>
                      </div>
                      <p className="mb-1">
                        <strong>Issuer:</strong> {cert.issuer || 'Unknown'}
                      </p>
                      <small>
                        <strong>Expires:</strong> {
                          cert.expiryDate 
                            ? new Date(cert.expiryDate).toLocaleDateString() 
                            : 'Unknown'
                        }
                      </small>
                      <br />
                      <small>
                        <strong>Private Key:</strong> {cert.hasPrivateKey ? '✅ Yes' : '❌ No'} |{' '}
                        <strong>Client Auth:</strong> {cert.isValidForClientAuth ? '✅ Yes' : '❌ No'}
                      </small>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Certificate Details */}
            <div className="col-md-6">
              <h4>Certificate Details</h4>
              {selectedCert ? (
                <div className="card">
                  <div className="card-body">
                    <h5 className="card-title">
                      {selectedCert.subject?.split(',')[0]?.replace('CN=', '') || 'Unknown Certificate'}
                    </h5>
                    <div className="mb-2">
                      <strong>Status:</strong>{' '}
                      <span className={`badge ${
                        selectedCert.isValidForClientAuth ? 'bg-success' : 'bg-warning'
                      }`}>
                        {selectedCert.statusDescription || 'Unknown'}
                      </span>
                    </div>
                    <p><strong>Subject:</strong> {selectedCert.subject}</p>
                    <p><strong>Issuer:</strong> {selectedCert.issuer}</p>
                    <p><strong>Expires:</strong> {new Date(selectedCert.expiryDate).toLocaleString()}</p>
                    <p><strong>Thumbprint:</strong></p>
                    <code style={{ fontSize: '0.8em', wordBreak: 'break-all' }}>
                      {selectedCert.thumbprint}
                    </code>
                    <div className="mt-3">
                      <div className="form-check">
                        <input 
                          className="form-check-input" 
                          type="checkbox" 
                          checked={selectedCert.hasPrivateKey} 
                          disabled 
                        />
                        <label className="form-check-label">Has Private Key</label>
                      </div>
                      <div className="form-check">
                        <input 
                          className="form-check-input" 
                          type="checkbox" 
                          checked={selectedCert.isValidForClientAuth} 
                          disabled 
                        />
                        <label className="form-check-label">Valid for Client Authentication</label>
                      </div>
                    </div>
                  </div>
                </div>
              ) : (
                <div className="alert alert-info">
                  Select a certificate from the list to view details.
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default CertificateTest;