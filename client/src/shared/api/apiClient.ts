import { env } from '../../config/env';
import { ApiClientError, getApiErrorMessage } from './apiError';
import type { ApiErrorResponse } from './apiResponse.types';
import { tokenStorage } from './tokenStorage';

type RequestOptions = Omit<RequestInit, 'body'> & {
  body?: BodyInit | unknown;
  auth?: boolean;
};

export async function apiClient<T>(path: string, options: RequestOptions = {}) {
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

  const response = await fetch(`${env.apiBaseUrl}${path}`, {
    ...options,
    headers,
    body: requestBody
  });

  const payload = await response.json().catch(() => null);

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
