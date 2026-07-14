import { beforeEach, describe, expect, it, vi } from 'vitest';
import { apiClient } from './apiClient';
import { tokenStorage } from './tokenStorage';
import { createMediaUploadSession, uploadFileViaMediaWorkflow } from './media';

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

  it('uploads binary to backend media route and returns finalized media asset response', async () => {
    const file = new File(['image'], 'avatar.jpg', { type: 'image/jpeg' });

    apiClientMock
      .mockResolvedValueOnce({
        data: {
          mediaAssetId: 'asset-123',
          uploadUrl: '/api/uploads/asset-123',
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
      'http://localhost:5294/api/uploads/asset-123',
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
});
