import { apiClient } from './apiClient';
import { ENDPOINTS } from './endpoints';
import type { ApiResponse } from './apiResponse.types';

export interface PrivateMediaDownloadUrlResponse {
  url: string;
  expiresAtUtc: string;
  deliveryMode: 'signed-url' | 'backend-route';
}

export function buildPrivateMediaViewUrl(mediaAssetId: string): string {
  return ENDPOINTS.MEDIA.PRIVATE_BY_ID(mediaAssetId);
}

export function buildPrivateMediaDownloadRoute(mediaAssetId: string): string {
  return ENDPOINTS.MEDIA.PRIVATE_DOWNLOAD(mediaAssetId);
}

export async function getPrivateMediaDownloadUrl(mediaAssetId: string): Promise<PrivateMediaDownloadUrlResponse> {
  const response = await apiClient<ApiResponse<PrivateMediaDownloadUrlResponse>>(
    ENDPOINTS.MEDIA.PRIVATE_DOWNLOAD_URL(mediaAssetId),
    { auth: true }
  );

  return response.data;
}
