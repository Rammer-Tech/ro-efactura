import React, { useState } from 'react';
import CertificateTest from './components/CertificateTest';
import AnafAuthTest from './components/AnafAuthTest';
import 'bootstrap/dist/css/bootstrap.min.css';

function App() {
  const [activeTab, setActiveTab] = useState('certificates');

  return (
    <div className="App">
      {/* Navigation */}
      <nav className="navbar navbar-expand-lg navbar-dark bg-primary">
        <div className="container">
          <span className="navbar-brand">🇷🇴 Romanian Certificate Authentication Test</span>
          <div className="navbar-nav">
            <button 
              className={`nav-link btn btn-link ${activeTab === 'certificates' ? 'active text-warning' : 'text-white'}`}
              onClick={() => setActiveTab('certificates')}
            >
              📜 Certificates
            </button>
            <button 
              className={`nav-link btn btn-link ${activeTab === 'auth' ? 'active text-warning' : 'text-white'}`}
              onClick={() => setActiveTab('auth')}
            >
              🔐 ANAF Auth
            </button>
          </div>
        </div>
      </nav>

      {/* Content */}
      <div className="container-fluid">
        {activeTab === 'certificates' && <CertificateTest />}
        {activeTab === 'auth' && <AnafAuthTest />}
      </div>

      {/* Footer */}
      <footer className="mt-5 py-4 bg-light text-center">
        <div className="container">
          <small className="text-muted">
            Romanian Certificate Authentication Test Environment • 
            Backend: <code>https://localhost:7205</code> • 
            Frontend: <code>http://localhost:3000</code>
          </small>
        </div>
      </footer>
    </div>
  );
}

export default App;