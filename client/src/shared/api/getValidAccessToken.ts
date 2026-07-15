import { tokenStorage } from './tokenStorage';
import { env } from '../../config/env';

export async function getValidAccessToken(): Promise<string | null> {
  const token = tokenStorage.getAccessToken();
  if (!token) return null;

  if (isTokenExpired(token)) {
    const refreshToken = tokenStorage.getRefreshToken();
    if (!refreshToken) {
      tokenStorage.clear();
      return null;
    }

    try {
      const response = await fetch(`${env.apiBaseUrl}/api/auth/refresh-token`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ refreshToken })
      });

      if (!response.ok) {
        tokenStorage.clear();
        return null;
      }

      const payload = await response.json().catch(() => null);
      const tokens = payload?.data as { accessToken?: string; refreshToken?: string } | undefined;
      if (!tokens?.accessToken || !tokens.refreshToken) {
        tokenStorage.clear();
        return null;
      }

      tokenStorage.setTokens(tokens.accessToken, tokens.refreshToken);
      return tokens.accessToken;
    } catch {
      return token;
    }
  }

  return token;
}

function isTokenExpired(token: string): boolean {
  try {
    const payloadBase64 = token.split('.')[1];
    const decodedJson = atob(payloadBase64.replace(/-/g, '+').replace(/_/g, '/'));
    const payload = JSON.parse(decodedJson) as { exp?: number };
    if (!payload.exp) return false;
    
    // Check if token expires in the next 30 seconds
    const now = Math.floor(Date.now() / 1000);
    return payload.exp < now + 30;
  } catch {
    return true;
  }
}
