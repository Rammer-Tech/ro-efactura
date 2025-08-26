import axios from 'axios';

const API_BASE_URL = 'http://localhost:7205/api/test';

// Create axios instance with default config
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('API Error:', error);
    return Promise.reject(error);
  }
);

export const anafService = {
  // Certificate operations
  async getAvailableCertificates() {
    const response = await apiClient.get('/certificates');
    return response.data;
  },

  async getCertificateInfo(thumbprint) {
    const response = await apiClient.get(`/certificates/${thumbprint}`);
    return response.data;
  },

  async clearCertificateCache() {
    const response = await apiClient.post('/certificates/clear-cache');
    return response.data;
  },

  async certificateHealthCheck() {
    const response = await apiClient.get('/certificates/health');
    return response.data;
  },

  // ANAF authentication operations
  async testAnafAuthentication(clientId, clientSecret, callbackUrl) {
    const response = await apiClient.post('/anaf/authenticate', {
      clientId,
      clientSecret,
      callbackUrl
    });
    return response.data;
  },

  async testAnafAuthenticationWithCert(thumbprint, clientId, clientSecret, callbackUrl) {
    const response = await apiClient.post(`/anaf/authenticate/${thumbprint}`, {
      clientId,
      clientSecret,
      callbackUrl
    });
    return response.data;
  },

  async mockTokenExchange(code, clientId, clientSecret, redirectUri) {
    const response = await apiClient.post('/anaf/token-exchange', {
      code,
      clientId,
      clientSecret,
      redirectUri
    });
    return response.data;
  },

  async generateAuthUrl(clientId, callbackUrl) {
    const response = await apiClient.post('/anaf/auth-url', {
      clientId,
      clientSecret: 'dummy', // Not needed for URL generation
      callbackUrl
    });
    return response.data;
  },

  async anafHealthCheck() {
    const response = await apiClient.get('/anaf/health');
    return response.data;
  }
};

// OAuth popup handler
export class AnafOAuthPopup {
  constructor(authUrl, onSuccess, onError) {
    this.authUrl = authUrl;
    this.onSuccess = onSuccess;
    this.onError = onError;
    this.popup = null;
    this.checkInterval = null;
  }

  start() {
    // Open popup
    this.popup = window.open(
      this.authUrl,
      'anaf-auth',
      'width=600,height=700,scrollbars=yes,resizable=yes,status=yes,location=yes'
    );

    if (!this.popup) {
      this.onError('Popup blocked. Please allow popups for this site.');
      return;
    }

    // Listen for messages from popup
    this.messageHandler = (event) => {
      if (event.origin !== window.location.origin) {
        return;
      }

      if (event.data.type === 'ANAF_AUTH_SUCCESS') {
        this.cleanup();
        this.onSuccess(event.data.code);
      } else if (event.data.type === 'ANAF_AUTH_ERROR') {
        this.cleanup();
        this.onError(event.data.error);
      }
    };

    window.addEventListener('message', this.messageHandler);

    // Check if popup is closed manually
    this.checkInterval = setInterval(() => {
      if (this.popup.closed) {
        this.cleanup();
        this.onError('Authentication cancelled by user');
      }
    }, 1000);
  }

  cleanup() {
    if (this.popup) {
      this.popup.close();
      this.popup = null;
    }
    if (this.checkInterval) {
      clearInterval(this.checkInterval);
      this.checkInterval = null;
    }
    if (this.messageHandler) {
      window.removeEventListener('message', this.messageHandler);
      this.messageHandler = null;
    }
  }
}

export default anafService;