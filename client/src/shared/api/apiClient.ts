import { env } from '../../config/env';
import { ApiClientError, getApiErrorMessage } from './apiError';
import type { ApiErrorResponse } from './apiResponse.types';
import { tokenStorage } from './tokenStorage';

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: BodyInit | unknown;
  auth?: boolean;
  skipAuthRefresh?: boolean;
};

export async function apiClient<T>(path: string, options: RequestOptions = {}) {
  const response = await sendRequest(path, options);
  let payload = await response.json().catch(() => null);

  if (
    response.status === 401 &&
    options.auth &&
    !options.skipAuthRefresh &&
    path !== '/api/auth/refresh-token'
  ) {
    const refreshed = await refreshAccessToken();

    if (refreshed) {
      const retryResponse = await sendRequest(path, options);
      payload = await retryResponse.json().catch(() => null);

      if (retryResponse.ok) {
        return payload as T;
      }

      const retryError = payload as ApiErrorResponse | null;
      throw new ApiClientError(getApiErrorMessage(retryError), {
        errorCode: retryError?.errorCode,
        details: retryError?.details,
        status: retryResponse.status,
        response: retryError
      });
    }
  }

  if (!response.ok) {
    const error = payload as ApiErrorResponse | null;
    throw new ApiClientError(getApiErrorMessage(error), {
      errorCode: error?.errorCode,
      details: error?.details,
      status: response.status,
      response: error
    });
  }

  return payload as T;
}

async function sendRequest(path: string, options: RequestOptions = {}) {
  const headers = new Headers(options.headers);
  const isFormData = options.body instanceof FormData;
  const requestBody: BodyInit | undefined = isFormData
    ? (options.body as BodyInit)
    : options.body
      ? JSON.stringify(options.body)
      : undefined;

  if (!isFormData) {
    headers.set('Content-Type', 'application/json');
  }

  if (options.auth) {
    const accessToken = tokenStorage.getAccessToken();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }
  }

  return fetch(`${env.apiBaseUrl}${path}`, {
    ...options,
    headers,
    body: requestBody
  });
}

async function refreshAccessToken() {
  const refreshToken = tokenStorage.getRefreshToken();
  if (!refreshToken) {
    tokenStorage.clear();
    return false;
  }

  const headers = new Headers();
  headers.set('Content-Type', 'application/json');

  const response = await fetch(`${env.apiBaseUrl}/api/auth/refresh-token`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ refreshToken })
  });

  const payload = await response.json().catch(() => null);
  if (!response.ok) {
    tokenStorage.clear();
    return false;
  }

  const tokens = payload?.data as { accessToken?: string; refreshToken?: string } | undefined;
  if (!tokens?.accessToken || !tokens.refreshToken) {
    tokenStorage.clear();
    return false;
  }

  tokenStorage.setTokens(tokens.accessToken, tokens.refreshToken);
  return true;
}
