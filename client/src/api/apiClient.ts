import axios from 'axios';

const TOKEN_KEY = 'srp_access_token';
const DEV_USER_KEY = 'srp_dev_user_id';

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '',
  headers: {
    Accept: 'application/json'
  }
});

apiClient.interceptors.request.use((config) => {
  const token = localStorage.getItem(TOKEN_KEY);
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  const devUserId = localStorage.getItem(DEV_USER_KEY);
  if (devUserId) {
    config.headers['X-Dev-User-Id'] = devUserId;
  }

  return config;
});

export function setAccessToken(token: string | null) {
  if (token) {
    localStorage.setItem(TOKEN_KEY, token);
  } else {
    localStorage.removeItem(TOKEN_KEY);
  }
}

export function setDevUserId(userId: string | null) {
  if (userId) {
    localStorage.setItem(DEV_USER_KEY, userId);
  } else {
    localStorage.removeItem(DEV_USER_KEY);
  }
}

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
}

export interface ApiErrorResponse {
  success: boolean;
  message: string;
  code: string;
  details?: unknown;
}
