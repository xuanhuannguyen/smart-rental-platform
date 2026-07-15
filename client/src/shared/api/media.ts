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
  HouseRule: 1,
  MeterReading: 2,
  ChatAttachment: 2,
};

export function buildPublicMediaViewUrl(mediaAssetId: string): string {
  return toAbsoluteUrl(ENDPOINTS.MEDIA.PUBLIC_BY_ID(mediaAssetId));
}

export function buildPrivateMediaViewUrl(mediaAssetId: string): string {
  return ENDPOINTS.MEDIA.PRIVATE_BY_ID(mediaAssetId);
}

export function buildPrivateMediaDownloadRoute(mediaAssetId: string): string {
  return ENDPOINTS.MEDIA.PRIVATE_DOWNLOAD(mediaAssetId);
}

export function extractPrivateMediaAssetId(source?: string | null): string | null {
  const match = source?.match(/\/api\/media\/private\/([0-9a-f-]{36})(?:\/download)?(?:[?#]|$)/i);
  return match?.[1] ?? null;
}

export async function getPrivateMediaBlob(mediaAssetId: string): Promise<Blob> {
  return apiClient<Blob>(
    ENDPOINTS.MEDIA.PRIVATE_BY_ID(mediaAssetId),
    { auth: true, responseType: 'blob' }
  );
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
  try {
    await putMediaBinary(session.uploadUrl, session.httpMethod, file);
  } catch (error) {
    if (session.deliveryMode !== 'signed-upload-url' || isApiUrl(session.uploadUrl)) {
      throw error;
    }

    // A signed S3 upload can be blocked by bucket CORS before the PUT is sent.
    // Retry through the authenticated backend route so uploads remain usable.
    await putMediaBinary(
      ENDPOINTS.MEDIA.UPLOAD_BINARY(session.mediaAssetId),
      'PUT',
      file
    );
  }
}

async function putMediaBinary(
  uploadUrl: string,
  httpMethod: string,
  file: File
): Promise<void> {
  const headers = new Headers();
  headers.set('Content-Type', file.type || 'application/octet-stream');

  const absoluteUploadUrl = toAbsoluteUrl(uploadUrl);
  if (isApiUrl(uploadUrl)) {
    const accessToken = tokenStorage.getAccessToken();
    if (accessToken) {
      headers.set('Authorization', `Bearer ${accessToken}`);
    }
  }

  const response = await fetch(absoluteUploadUrl, {
    method: httpMethod || 'PUT',
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
  if (url.startsWith('/api/') || url.startsWith('api/')) {
    return true;
  }

  try {
    const targetUrl = new URL(url);
    const apiBaseUrl = new URL(env.apiBaseUrl);
    return targetUrl.origin === apiBaseUrl.origin && targetUrl.pathname.startsWith('/api/');
  } catch {
    return false;
  }
}

