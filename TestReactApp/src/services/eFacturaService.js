import axios from 'axios';

const API_BASE_URL = 'http://localhost:5126/api/integrations/einvoice';

// Create axios instance with default config
const apiClient = axios.create({
  baseURL: API_BASE_URL,
  withCredentials: true, // Important for session cookies
  headers: {
    'Content-Type': 'application/json'
  }
});

// Add response interceptor for error handling
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    console.error('eFactura API Error:', error);
    return Promise.reject(error);
  }
);

export const eFacturaService = {
  /**
   * Initiate OAuth flow - exactly like SmartBill
   * Returns the authorization URL to redirect to
   */
  async initiateOAuth() {
    try {
      const response = await apiClient.post('/initiate');
      return response.data;
    } catch (error) {
      console.error('Failed to initiate OAuth:', error);
      throw error;
    }
  },

  /**
   * Get current authorization status
   */
  async getAuthStatus() {
    try {
      const response = await apiClient.get('/status');
      return response.data;
    } catch (error) {
      console.error('Failed to get auth status:', error);
      throw error;
    }
  },

  /**
   * Clear stored authorization
   */
  async clearAuthorization() {
    try {
      const response = await apiClient.delete('/clear');
      return response.data;
    } catch (error) {
      console.error('Failed to clear authorization:', error);
      throw error;
    }
  },

  /**
   * Exchange authorization code for token (for manual testing)
   */
  async exchangeCode(code, state = null) {
    try {
      const response = await apiClient.post('/exchange-code', {
        code,
        state
      });
      return response.data;
    } catch (error) {
      console.error('Failed to exchange code:', error);
      throw error;
    }
  }
};

export default eFacturaService;