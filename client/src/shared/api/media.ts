import { env } from '../../config/env';
import { apiClient } from './apiClient';
import { ENDPOINTS } from './endpoints';
import type { ApiResponse } from './apiResponse.types';
import { tokenStorage } from './tokenStorage';

export type MediaWorkflowScope =
  | 'RoomingHouse'
  | 'Room'
  | 'LegalDocument'
  | 'KycDocument'
  | 'Avatar'
  | 'HouseRule'
  | 'MeterReading'
  | 'ChatAttachment';

export interface MediaUploadSessionResponse {
  mediaAssetId: string;
  uploadUrl: string;
  httpMethod: string;
  deliveryMode: string;
  expiresAtUtc: string;
}

export interface MediaAssetActionResponse {
  mediaAssetId: string;
  status: string;
  viewUrl?: string | null;
  downloadUrl?: string | null;
}

export interface MediaWorkflowUploadResult {
  mediaAssetId: string;
  url: string;
  status: string;
}

export interface PrivateMediaDownloadUrlResponse {
  url: string;
  expiresAtUtc: string;
  deliveryMode: 'signed-url' | 'backend-route';
}

const MEDIA_SCOPE_BY_UPLOAD_SCOPE: Record<MediaWorkflowScope, number> = {
  RoomingHouse: 1,
  Room: 2,
  LegalDocument: 3,
  KycDocument: 4,
  Avatar: 10,
  HouseRule: 9,
  MeterReading: 7,
  ChatAttachment: 8,
};

const MEDIA_VISIBILITY_BY_UPLOAD_SCOPE: Record<MediaWorkflowScope, number> = {
  RoomingHouse: 1,
  Room: 1,
  LegalDocument: 2,
  KycDocument: 2,
  Avatar: 1,
  HouseRule: 2,
  MeterReading: 2,
  ChatAttachment: 2,
};

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

export async function createMediaUploadSession(file: File, scope: MediaWorkflowScope): Promise<MediaUploadSessionResponse> {
  const response = await apiClient<ApiResponse<MediaUploadSessionResponse>>(
    ENDPOINTS.MEDIA.UPLOAD_URL,
    {
      method: 'POST',
      auth: true,
      body: {
        originalFileName: file.name,
        contentType: file.type || 'application/octet-stream',
        fileSize: file.size,
        scope: MEDIA_SCOPE_BY_UPLOAD_SCOPE[scope],
        visibility: MEDIA_VISIBILITY_BY_UPLOAD_SCOPE[scope],
      }
    }
  );

  return response.data;
}

export async function finalizeMediaUpload(
  mediaAssetId: string,
  fileHash?: string | null
): Promise<MediaAssetActionResponse> {
  const response = await apiClient<ApiResponse<MediaAssetActionResponse>>(
    ENDPOINTS.MEDIA.FINALIZE,
    {
      method: 'POST',
      auth: true,
      body: {
        mediaAssetId,
        fileHash: fileHash ?? null,
      }
    }
  );

  return response.data;
}

export async function uploadFileViaMediaWorkflow(
  file: File,
  scope: MediaWorkflowScope
): Promise<MediaWorkflowUploadResult> {
  const session = await createMediaUploadSession(file, scope);
  await uploadBinaryToMediaSession(session, file);
  const finalized = await finalizeMediaUpload(session.mediaAssetId);

  return {
    mediaAssetId: finalized.mediaAssetId,
    url: finalized.viewUrl || finalized.downloadUrl || '',
    status: finalized.status,
  };
}

async function uploadBinaryToMediaSession(
  session: MediaUploadSessionResponse,
  file: File
): Promise<void> {
  const headers = new Headers();
  headers.set('Content-Type', file.type || 'application/octet-stream');

  const uploadUrl = toAbsoluteUrl(session.uploadUrl);
  if (isApiUrl(session.uploadUrl)) {
    const accessToken = tokenStorage.getAccessToken();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }
  }

  const response = await fetch(uploadUrl, {
    method: session.httpMethod || 'PUT',
    headers,
    body: file,
  });

  if (!response.ok) {
    throw new Error('Không thể tải tệp lên storage.');
  }
}

function toAbsoluteUrl(url: string): string {
  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  return `${env.apiBaseUrl}${url.startsWith('/') ? url : `/${url}`}`;
}

function isApiUrl(url: string): boolean {
  return url.startsWith('/api/') || url.startsWith('api/');
}

