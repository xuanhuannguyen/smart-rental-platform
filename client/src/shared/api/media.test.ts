import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from './apiClient';
import { tokenStorage } from './tokenStorage';
import { buildPublicMediaViewUrl, createMediaUploadSession, extractPrivateMediaAssetId, getPrivateMediaBlob, uploadFileViaMediaWorkflow } from './media';

vi.mock('../../config/env', () => ({
  env: {
    apiBaseUrl: 'http://localhost:5294',
  }
}));

vi.mock('./apiClient', () => ({
  apiClient: vi.fn(),
}));

vi.mock('./tokenStorage', () => ({
  tokenStorage: {
    getAccessToken: vi.fn(),
    getRefreshToken: vi.fn(),
    getTokens: vi.fn(),
    setTokens: vi.fn(),
    clear: vi.fn(),
  }
}));

describe('media workflow api', () => {
  const apiClientMock = vi.mocked(apiClient);
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.stubGlobal('fetch', fetchMock);
  });

  it('maps KycDocument uploads to private KYC media workflow scope', async () => {
    const file = new File(['image'], 'kyc-front.jpg', { type: 'image/jpeg' });

    apiClientMock.mockResolvedValue({
      data: {
        mediaAssetId: 'asset-id',
        uploadUrl: '/api/uploads/asset-id',
        httpMethod: 'PUT',
        deliveryMode: 'backend-route',
        expiresAtUtc: '2026-07-14T00:00:00Z',
      }
    } as never);

    const session = await createMediaUploadSession(file, 'KycDocument');

    expect(session.mediaAssetId).toBe('asset-id');
    expect(apiClientMock).toHaveBeenCalledWith(
      '/api/media/upload-url',
      expect.objectContaining({
        method: 'POST',
        auth: true,
        body: expect.objectContaining({
          originalFileName: 'kyc-front.jpg',
          contentType: 'image/jpeg',
          fileSize: file.size,
          scope: 4,
          visibility: 2,
        })
      })
    );
  });

  it('maps HouseRule uploads to public media and builds an absolute public URL', async () => {
    const file = new File(['pdf'], 'house-rule.pdf', { type: 'application/pdf' });

    apiClientMock.mockResolvedValue({
      data: {
        mediaAssetId: 'house-rule-asset',
        uploadUrl: '/api/uploads/house-rule-asset',
        httpMethod: 'PUT',
        deliveryMode: 'backend-route',
        expiresAtUtc: '2026-07-15T00:00:00Z',
      }
    } as never);

    await createMediaUploadSession(file, 'HouseRule');

    expect(apiClientMock).toHaveBeenCalledWith(
      '/api/media/upload-url',
      expect.objectContaining({
        body: expect.objectContaining({
          scope: 9,
          visibility: 1,
        })
      })
    );
    expect(buildPublicMediaViewUrl('house-rule-asset'))
      .toBe('http://localhost:5294/api/media/public/house-rule-asset');
  });

  it('uploads binary to backend media route and returns finalized media asset response', async () => {
    const file = new File(['image'], 'avatar.jpg', { type: 'image/jpeg' });

    apiClientMock
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'asset-123',
          uploadUrl: 'http://localhost:5294/api/media/upload/asset-123',
          httpMethod: 'PUT',
          deliveryMode: 'backend-route',
          expiresAtUtc: '2026-07-14T00:00:00Z',
        }
      } as never)
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'asset-123',
          status: 'Linked',
          viewUrl: '/api/media/private/asset-123',
          downloadUrl: null,
        }
      } as never);

    vi.mocked(tokenStorage.getAccessToken).mockReturnValue('access-token');
    fetchMock.mockResolvedValue(new Response(null, { status: 200 }));

    const result = await uploadFileViaMediaWorkflow(file, 'KycDocument');

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith(
      'http://localhost:5294/api/media/upload/asset-123',
      expect.objectContaining({
        method: 'PUT',
        body: file,
      })
    );

    const requestInit = fetchMock.mock.calls[0]?.[1];
    const headers = requestInit?.headers as Headers;
    expect(headers.get('Content-Type')).toBe('image/jpeg');
    expect(headers.get('Authorization')).toBe('Bearer access-token');

    expect(result).toEqual({
      mediaAssetId: 'asset-123',
      url: '/api/media/private/asset-123',
      status: 'Linked',
    });
  });

  it('falls back to the backend upload route when a signed upload is blocked', async () => {
    const file = new File(['image'], 'rooming-house.png', { type: 'image/png' });

    apiClientMock
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'asset-s3-fallback',
          uploadUrl: 'https://media-bucket.s3.amazonaws.com/public/rooming-house.png?signature=test',
          httpMethod: 'PUT',
          deliveryMode: 'signed-upload-url',
          expiresAtUtc: '2026-07-15T00:00:00Z',
        }
      } as never)
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'asset-s3-fallback',
          status: 'Uploaded',
          viewUrl: '/api/media/public/asset-s3-fallback',
          downloadUrl: null,
        }
      } as never);

    vi.mocked(tokenStorage.getAccessToken).mockReturnValue('access-token');
    fetchMock
      .mockRejectedValueOnce(new TypeError('Failed to fetch'))
      .mockResolvedValueOnce(new Response(null, { status: 204 }));

    const result = await uploadFileViaMediaWorkflow(file, 'RoomingHouse');

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(fetchMock.mock.calls[0]?.[0]).toContain('media-bucket.s3.amazonaws.com');
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      'http://localhost:5294/api/media/upload/asset-s3-fallback'
    );

    const fallbackHeaders = fetchMock.mock.calls[1]?.[1]?.headers as Headers;
    expect(fallbackHeaders.get('Authorization')).toBe('Bearer access-token');
    expect(fallbackHeaders.get('Content-Type')).toBe('image/png');
    expect(result).toEqual({
      mediaAssetId: 'asset-s3-fallback',
      url: '/api/media/public/asset-s3-fallback',
      status: 'Uploaded',
    });
  });

  it('extracts private media ids and loads them through an authenticated blob request', async () => {
    const mediaAssetId = '10483f9c-dc4b-4656-b09c-de36754e9397';
    const blob = new Blob(['private-image'], { type: 'image/jpeg' });
    apiClientMock.mockResolvedValue(blob as never);

    expect(extractPrivateMediaAssetId(`/api/media/private/${mediaAssetId}`)).toBe(mediaAssetId);
    expect(extractPrivateMediaAssetId(`http://localhost:5294/api/media/private/${mediaAssetId}/download`)).toBe(mediaAssetId);
    await expect(getPrivateMediaBlob(mediaAssetId)).resolves.toBe(blob);
    expect(apiClientMock).toHaveBeenCalledWith(
      `/api/media/private/${mediaAssetId}`,
      { auth: true, responseType: 'blob' }
    );
  });
});
